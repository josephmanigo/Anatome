using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace ThoraxLab.Learning
{
    public sealed class AtomicFileStore<T>
    {
        readonly string path;
        readonly Func<T, string> encode;
        readonly Func<string, T> decode;
        readonly Action<T> validate;
        readonly object gate = new object();
        public bool RecoveredBackup { get; private set; }
        public string FilePath => path;

        public AtomicFileStore(string path, Func<T, string> encode, Func<string, T> decode, Action<T> validate)
        {
            this.path = path; this.encode = encode; this.decode = decode; this.validate = validate;
        }

        public T Load(Func<T> empty)
        {
            lock (gate)
            {
                RecoveredBackup = false;
                if (!File.Exists(path) && !File.Exists(path + ".bak")) return empty();
                if (TryRead(path, out T value)) return value;
                if (TryRead(path + ".bak", out value)) { RecoveredBackup = true; return value; }
                throw new InvalidDataException("Both saved copies could not be verified. Original files have been preserved.");
            }
        }

        public void Save(T value)
        {
            lock (gate)
            {
                validate(value);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string body = encode(value);
                var bytes = Encoding.UTF8.GetBytes("THORAX-SAVE-1\n" + Hash(body) + "\n" + body);
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                    // Keep a known-good backup, including when recovering a corrupt primary.
                    if (TryRead(path, out _)) File.Copy(path, path + ".bak", true);
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                    if (!TryRead(path + ".bak", out _)) File.Copy(path, path + ".bak", true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        bool TryRead(string candidate, out T value)
        {
            value = default;
            try
            {
                if (!File.Exists(candidate) || new FileInfo(candidate).Length > 64 * 1024 * 1024) return false;
                string content = File.ReadAllText(candidate, Encoding.UTF8);
                int first = content.IndexOf('\n'), second = content.IndexOf('\n', first + 1);
                if (first < 0 || second < 0 || content.Substring(0, first) != "THORAX-SAVE-1") return false;
                string body = content.Substring(second + 1);
                if (Hash(body) != content.Substring(first + 1, second - first - 1)) return false;
                value = decode(body); validate(value); return true;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is InvalidDataException || e is FormatException) { return false; }
        }

        public static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
    }
}
