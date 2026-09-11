using Domain.Shared;

namespace Domain.UnitTests.Shared;

public class ValidationErrorTests
{
    [Fact]
    public void Constructor_ShouldKeepEveryCause_WhenSeveralFieldsWereRejected()
    {
        // Arrange
        FieldError[] causes =
        [
            new("title", "recipes.invalid_title", "A title is required."),
            new("yield", "recipes.invalid_yield", "The yield must be greater than zero.")
        ];

        // Act
        var error = new ValidationError(causes);

        // Assert
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(ValidationError.ValidationCode, error.Code);
        Assert.Equal(["title", "yield"], error.Errors.OfType<FieldError>().Select(e => e.Field));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenThereIsNoCause()
    {
        // Arrange
        Error[] none = [];

        // Act
        ValidationError Act() => new(none);

        // Assert
        Assert.Throws<ArgumentException>(Act);
    }

    [Fact]
    public void FieldError_ShouldBeAValidationError_WhenConstructed()
    {
        // Arrange
        var error = new FieldError("email", "users.invalid_email", "That is not an email address.");

        // Act
        var type = error.Type;

        // Assert
        Assert.Equal(ErrorType.Validation, type);
    }
}
