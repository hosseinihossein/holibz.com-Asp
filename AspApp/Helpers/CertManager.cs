using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AspApp.Helpers;

public class CertManager
{
    public string CertsDirPath { get; }

    private string EcdsaSigningCertPath { get; }
    private string EcdsaKeyEncryptionCertPath { get; }

    private string RsaSigningCertPath { get; }
    private string RsaKeyEncryptionCertPath { get; }


    public CertManager(IWebHostEnvironment hostEnv)
    {
        CertsDirPath = Path.Combine(hostEnv.ContentRootPath, "Certs");
        Directory.CreateDirectory(CertsDirPath);

        EcdsaSigningCertPath = Path.Combine(CertsDirPath, "EcdsaSigningCert.pfx");
        EcdsaKeyEncryptionCertPath = Path.Combine(CertsDirPath, "EcdsaKeyEncryptionCert.pfx");

        RsaSigningCertPath = Path.Combine(CertsDirPath, "RsaSigningCert.pfx");
        RsaKeyEncryptionCertPath = Path.Combine(CertsDirPath, "RsaKeyEncryptionCert.pfx");
    }


    // **********    Ecdsa    **********
    public async Task<X509Certificate2> GetEcdsaSigningCert()
    {
        if (!File.Exists(EcdsaSigningCertPath))
        {
            // Create and save the Ecdsa signing cert
            byte[] certBytes = GenerateAndExportEcdsaSigningCert(string.Empty);
            await File.WriteAllBytesAsync(EcdsaSigningCertPath, certBytes);
        }

        return X509CertificateLoader.LoadPkcs12FromFile(
            EcdsaSigningCertPath,
            string.Empty,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable
        );
    }
    private byte[] GenerateAndExportEcdsaSigningCert(string password)
    {
        // 1. Create an ECDSA key pair
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // 2. Define certificate subject
        var subject = new X500DistinguishedName("CN=EcdsaSigningCertificate");

        // 3. Create certificate request
        var request = new CertificateRequest(
            subject,
            ecdsa,
            HashAlgorithmName.SHA256
        );

        // Optional: Add extensions
        //request.CertificateExtensions.Add(
        //    new X509BasicConstraintsExtension(false, false, 0, false)
        //);
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,// | X509KeyUsageFlags.KeyEncipherment,
                critical: true
            )
        );
        //request.CertificateExtensions.Add(
        //    new X509SubjectKeyIdentifierExtension(request.PublicKey, false)
        //);

        using X509Certificate2 ecdsaSigningCert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(2)
        );

        return ecdsaSigningCert.Export(X509ContentType.Pfx, password);
    }

    public async Task<X509Certificate2> GetEcdsaKeyEncryptionCert()
    {
        if (!File.Exists(EcdsaKeyEncryptionCertPath))
        {
            // Create and save the Ecdsa KeyEncryption cert
            byte[] certBytes = GenerateAndExportEcdsaKeyEncryptionCert(string.Empty);
            await File.WriteAllBytesAsync(EcdsaKeyEncryptionCertPath, certBytes);
        }

        return X509CertificateLoader.LoadPkcs12FromFile(
            EcdsaKeyEncryptionCertPath,
            string.Empty,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable
        );
    }
    private byte[] GenerateAndExportEcdsaKeyEncryptionCert(string password)
    {
        // 1. Create an ECDSA key pair
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // 2. Define certificate subject
        var subject = new X500DistinguishedName("CN=EcdsaKeyEncryptionCertificate");

        // 3. Create certificate request
        var request = new CertificateRequest(
            subject,
            ecdsa,
            HashAlgorithmName.SHA256
        );

        // Optional: Add extensions
        //request.CertificateExtensions.Add(
        //    new X509BasicConstraintsExtension(false, false, 0, false)
        //);
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyEncipherment,
                critical: true
            )
        );
        //request.CertificateExtensions.Add(
        //    new X509SubjectKeyIdentifierExtension(request.PublicKey, false)
        //);

        using X509Certificate2 ecdsaKeyEncryptionCert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(2)
        );

        return ecdsaKeyEncryptionCert.Export(X509ContentType.Pfx, password);
    }



    // **********    Rsa    **********
    public async Task<X509Certificate2> GetRsaSigningCert()
    {
        if (!File.Exists(RsaSigningCertPath))
        {
            // Create, export, and save the Rsa signing cert
            // We wrap the generation and export together to handle key lifetime safely
            byte[] certBytes = GenerateAndExportRsaSigningCert(string.Empty);
            await File.WriteAllBytesAsync(RsaSigningCertPath, certBytes);
        }

        // Use EphemeralKeySet for modern, fast, in-memory loading (great for containers/cloud)
        // Add Exportable if your application needs to export this cert again after loading it
        return X509CertificateLoader.LoadPkcs12FromFile(
            RsaSigningCertPath,
            string.Empty,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable
        );
    }

    private byte[] GenerateAndExportRsaSigningCert(string password)
    {
        // Do NOT dispose the RSA algorithm before exporting, 
        // as it owns the private key needed for the PFX generation.
        using var algorithm = RSA.Create(keySizeInBits: 4096);

        var subject = new X500DistinguishedName("CN=RsaSigningCertificate");
        var request = new CertificateRequest(
            subject,
            algorithm,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true
            )
        );

        using X509Certificate2 rsaSigningCert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(2)
        );

        // Export the certificate directly while the RSA key is guaranteed alive
        return rsaSigningCert.Export(X509ContentType.Pfx, password);
    }


    public async Task<X509Certificate2> GetRsaKeyEncryptionCert()
    {
        if (!File.Exists(RsaKeyEncryptionCertPath))
        {
            // Create, export, and save the Rsa encryption cert
            // We wrap the generation and export together to handle key lifetime safely
            byte[] certBytes = GenerateAndExportRsaKeyEncryptionCert(string.Empty);
            await File.WriteAllBytesAsync(RsaKeyEncryptionCertPath, certBytes);
        }

        // Use EphemeralKeySet for modern, fast, in-memory loading (great for containers/cloud)
        // Add Exportable if your application needs to export this cert again after loading it
        return X509CertificateLoader.LoadPkcs12FromFile(
            RsaKeyEncryptionCertPath,
            string.Empty,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable
        );
    }

    private byte[] GenerateAndExportRsaKeyEncryptionCert(string password)
    {
        // Do NOT dispose the RSA algorithm before exporting, 
        // as it owns the private key needed for the PFX generation.
        using var algorithm = RSA.Create(keySizeInBits: 4096);

        var subject = new X500DistinguishedName("CN=RsaEncryptionCertificate");
        var request = new CertificateRequest(
            subject,
            algorithm,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyEncipherment,
                critical: true
            )
        );

        using X509Certificate2 rsaKeyEncryptionCert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(2)
        );

        // Export the certificate directly while the RSA key is guaranteed alive
        return rsaKeyEncryptionCert.Export(X509ContentType.Pfx, password);
    }




}