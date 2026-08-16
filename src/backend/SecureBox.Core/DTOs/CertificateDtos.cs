namespace SecureBox.Core.DTOs;

public record CertificateDto(
    Guid CertificateId,
    string Name,
    string? Description,
    string Thumbprint,
    string Subject,
    string Issuer,
    string SerialNumber,
    string Algorithm,
    int KeySize,
    DateTime NotBefore,
    DateTime NotAfter,
    string Status,
    bool IsForSigning,
    bool IsForEncryption,
    string UploadedBy,
    DateTime CreatedAt
);

public record UploadCertificateRequest(
    string Name,
    string? Description,
    byte[] CertificateFile,
    string? Password,
    bool IsForSigning = false,
    bool IsForEncryption = true
);

public record UpdateCertificateRequest(
    string? Name,
    string? Description
);

public record CertificateQueryParams(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Search = null
);

