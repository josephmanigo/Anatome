using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEngine;

namespace ThoraxLab.Learning
{
    [Serializable] public sealed class TrustedAccount
    {
        public string userId, email, salt, pinHash, blockedUntilUtc;
        public int failedAttempts;
        public AuthSession session;
    }

    public sealed class TrustedDeviceStore
    {
        readonly string root;
        public TrustedDeviceStore(string root) { this.root = root; Directory.CreateDirectory(root); }
        string PathFor(string id) { if (!Guid.TryParse(id, out _)) throw new ArgumentException("Invalid account."); return Path.Combine(root, id + ".device"); }
        public TrustedAccount[] Accounts()
        {
            return Directory.GetFiles(root, "*.device").Select(file => { try { return Read(file); } catch { return null; } }).Where(a => a != null).ToArray();
        }
        public void Enroll(AuthSession session, string pin)
        {
            if (pin == null || pin.Length != 6 || pin.Any(c => c < '0' || c > '9')) throw new ArgumentException("Choose a six-digit device PIN.");
            byte[] salt = new byte[24]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            Write(new TrustedAccount { userId = session.user.id, email = session.user.email, session = session, salt = Convert.ToBase64String(salt), pinHash = HashPin(pin, salt) });
        }
        public AuthSession Unlock(string userId, string pin)
        {
            var record = Read(PathFor(userId));
            if (DateTime.TryParse(record.blockedUntilUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var blocked) && DateTime.UtcNow < blocked) throw new InvalidOperationException("Too many PIN attempts. Wait a minute or sign in online.");
            string hash = HashPin(pin ?? "", Convert.FromBase64String(record.salt));
            int difference = hash.Length ^ record.pinHash.Length;
            for (int i = 0; i < Math.Min(hash.Length, record.pinHash.Length); i++) difference |= hash[i] ^ record.pinHash[i];
            if (difference != 0)
            {
                record.failedAttempts++; if (record.failedAttempts >= 5) { record.blockedUntilUtc = DateTime.UtcNow.AddMinutes(1).ToString("o"); record.failedAttempts = 0; }
                Write(record); throw new InvalidOperationException("Incorrect device PIN.");
            }
            record.failedAttempts = 0; record.blockedUntilUtc = null; Write(record); return record.session;
        }
        public void Refresh(AuthSession session)
        {
            string file = PathFor(session.user.id); if (!File.Exists(file)) return;
            var record = Read(file); record.session = session; Write(record);
        }
        public void Forget(string id) { string file = PathFor(id); if (File.Exists(file)) File.Delete(file); }
        TrustedAccount Read(string file) => JsonUtility.FromJson<TrustedAccount>(Encoding.UTF8.GetString(DeviceEncryption.Unprotect(File.ReadAllBytes(file))));
        void Write(TrustedAccount record)
        {
            string file = PathFor(record.userId), temp = file + ".tmp";
            byte[] bytes = DeviceEncryption.Protect(Encoding.UTF8.GetBytes(JsonUtility.ToJson(record)));
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(file)) File.Replace(temp, file, null); else File.Move(temp, file);
        }
        static string HashPin(string pin, byte[] salt)
        {
            using (var derive = new Rfc2898DeriveBytes(pin, salt, 200000, HashAlgorithmName.SHA256)) return Convert.ToBase64String(derive.GetBytes(32));
        }
    }

    public static class DeviceEncryption
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        const string Alias = "org.thoraxlab.education.trusted.v1";
        static AndroidJavaObject Key()
        {
            using (var type = new AndroidJavaClass("java.security.KeyStore"))
            {
                var store = type.CallStatic<AndroidJavaObject>("getInstance", "AndroidKeyStore"); store.Call("load", new object[] { null });
                if (!store.Call<bool>("containsAlias", Alias))
                {
                    using (var generatorClass = new AndroidJavaClass("javax.crypto.KeyGenerator"))
                    using (var generator = generatorClass.CallStatic<AndroidJavaObject>("getInstance", "AES", "AndroidKeyStore"))
                    using (var builder = new AndroidJavaObject("android.security.keystore.KeyGenParameterSpec$Builder", Alias, 3))
                    {
                        builder.Call<AndroidJavaObject>("setBlockModes", new object[] { new string[] { "GCM" } }).Dispose();
                        builder.Call<AndroidJavaObject>("setEncryptionPaddings", new object[] { new string[] { "NoPadding" } }).Dispose();
                        using (var spec = builder.Call<AndroidJavaObject>("build")) generator.Call("init", spec);
                        generator.Call<AndroidJavaObject>("generateKey").Dispose();
                    }
                }
                var key = store.Call<AndroidJavaObject>("getKey", Alias, null); store.Dispose(); return key;
            }
        }
        public static byte[] Protect(byte[] plain)
        {
            using (var type = new AndroidJavaClass("javax.crypto.Cipher"))
            using (var cipher = type.CallStatic<AndroidJavaObject>("getInstance", "AES/GCM/NoPadding"))
            using (var key = Key())
            {
                cipher.Call("init", 1, key); byte[] iv = cipher.Call<byte[]>("getIV"), encrypted = cipher.Call<byte[]>("doFinal", plain);
                return new[] { (byte)iv.Length }.Concat(iv).Concat(encrypted).ToArray();
            }
        }
        public static byte[] Unprotect(byte[] encrypted)
        {
            if (encrypted.Length < 30 || encrypted[0] < 8 || encrypted[0] > 32) throw new CryptographicException("Invalid trusted profile.");
            using (var type = new AndroidJavaClass("javax.crypto.Cipher"))
            using (var cipher = type.CallStatic<AndroidJavaObject>("getInstance", "AES/GCM/NoPadding"))
            using (var key = Key())
            using (var spec = new AndroidJavaObject("javax.crypto.spec.GCMParameterSpec", 128, encrypted.Skip(1).Take(encrypted[0]).ToArray()))
            {
                cipher.Call("init", 2, key, spec); return cipher.Call<byte[]>("doFinal", encrypted.Skip(1 + encrypted[0]).ToArray());
            }
        }
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential)] struct Blob { public int size; public IntPtr data; }
        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool CryptProtectData(ref Blob input, string description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool CryptUnprotectData(ref Blob input, out IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr value);
        public static byte[] Protect(byte[] plain) => Transform(plain, true);
        public static byte[] Unprotect(byte[] encrypted) => Transform(encrypted, false);
        static byte[] Transform(byte[] bytes, bool protect)
        {
            var input = new Blob { size = bytes.Length, data = Marshal.AllocHGlobal(bytes.Length) }; Blob output = default; IntPtr description = IntPtr.Zero;
            try
            {
                Marshal.Copy(bytes, 0, input.data, bytes.Length);
                bool ok = protect ? CryptProtectData(ref input, "Anatome trusted profile", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output) : CryptUnprotectData(ref input, out description, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
                if (!ok) throw new CryptographicException("Device protection failed: " + Marshal.GetLastWin32Error());
                var result = new byte[output.size]; Marshal.Copy(output.data, result, 0, output.size); return result;
            }
            finally { Marshal.FreeHGlobal(input.data); if (output.data != IntPtr.Zero) LocalFree(output.data); if (description != IntPtr.Zero) LocalFree(description); }
        }
#else
        public static byte[] Protect(byte[] plain) => throw new PlatformNotSupportedException("Trusted access requires Windows or Quest.");
        public static byte[] Unprotect(byte[] encrypted) => throw new PlatformNotSupportedException("Trusted access requires Windows or Quest.");
#endif
    }
}
