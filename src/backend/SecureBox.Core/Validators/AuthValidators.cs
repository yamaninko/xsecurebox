using FluentValidation;
using SecureBox.Core.DTOs;

namespace SecureBox.Core.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().WithMessage("Username is required");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required");
    }
}

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3).MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one number")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character");
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one number")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character")
            .Must((request, newPassword) => newPassword != request.CurrentPassword)
            .WithMessage("New password cannot be the same as the current password");

        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword).WithMessage("Passwords do not match");
    }
}

public class CreateKeyRequestValidator : AbstractValidator<CreateKeyRequest>
{
    public CreateKeyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.KeyType).NotEmpty();
        RuleFor(x => x.Value).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.CertificateId).NotEmpty();
        RuleFor(x => x.EnvironmentTag).NotEmpty();
    }
}

public class RetrieveKeyRequestValidator : AbstractValidator<RetrieveKeyRequest>
{
    public RetrieveKeyRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public class UploadCertificateRequestValidator : AbstractValidator<UploadCertificateRequest>
{
    public UploadCertificateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CertificateFile).NotNull().Must(f => f is { Length: > 0 }).WithMessage("Certificate file is required");
    }
}

public class RotateKeyRequestValidator : AbstractValidator<RotateKeyRequest>
{
    public RotateKeyRequestValidator()
    {
        RuleFor(x => x.NewValue).NotEmpty().MaximumLength(4096);
    }
}

public class RevokeKeyRequestValidator : AbstractValidator<RevokeKeyRequest>
{
    public RevokeKeyRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty();
    }
}
