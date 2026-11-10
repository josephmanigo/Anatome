import com.android.apksig.ApkVerifier;
import java.io.File;
import java.security.MessageDigest;
import java.security.cert.X509Certificate;
import java.util.HexFormat;

/** Checks the actual signed APK's rotation proof with Android's bundled verifier. */
class VerifySigningLineage {
    static String fingerprint(X509Certificate certificate) throws Exception {
        return HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(certificate.getEncoded()));
    }
    static void require(boolean valid, String message) {
        if (!valid) throw new IllegalStateException(message);
    }
    public static void main(String[] args) throws Exception {
        require(args.length == 3, "Expected APK, previous certificate SHA-256, release certificate SHA-256");
        for (int api = 29; api <= 36; api++) {
            var result = new ApkVerifier.Builder(new File(args[0])).setMinCheckedPlatformVersion(api).setMaxCheckedPlatformVersion(api).build().verify();
            require(result.isVerified() && result.isVerifiedUsingV3Scheme(), "V3 signature invalid for API " + api);
            require(result.getSignerCertificates().size() == 1 && fingerprint(result.getSignerCertificates().get(0)).equalsIgnoreCase(args[2]), "Wrong active release signer for API " + api);
            var lineage = result.getSigningCertificateLineage();
            require(lineage != null && lineage.size() == 2, "Expected two-certificate rotation proof");
            var certificates = lineage.getCertificatesInLineage();
            require(fingerprint(certificates.get(0)).equalsIgnoreCase(args[1]) && fingerprint(certificates.get(1)).equalsIgnoreCase(args[2]), "Rotation proof has wrong ancestry");
            var capabilities = lineage.getSignerCapabilities(certificates.get(0));
            require(capabilities.hasInstalledData() && !capabilities.hasRollback(), "Rotation must preserve installed data and forbid old-key rollback");
        }
        System.out.println("{\"passed\":true,\"androidApiLevels\":[29,30,31,32,33,34,35,36],\"activeSignerMatches\":true,\"rotationProofValid\":true,\"installedDataCapability\":true,\"oldKeyRollbackAllowed\":false,\"physicalInstallationTested\":false}");
    }
}
