using DlfVoting.Api.Validation;
using DlfVoting.Infrastructure;

namespace DlfVoting.Api.Tests.Tests;

public class SecurePasswordGeneratorTests
{
    private const int Samples = 1000;

    [Fact]
    public void UserPasswordLength_IsEight()
    {
        Assert.Equal(8, SecurePasswordGenerator.UserPasswordLength);
    }

    [Fact]
    public void Generate_ByDefault_ReturnsEightCharacters()
    {
        for (var i = 0; i < Samples; i++)
        {
            Assert.Equal(8, SecurePasswordGenerator.Generate().Length);
        }
    }

    [Fact]
    public void Generate_ByDefault_AlwaysMeetsTheUserPasswordRule()
    {
        for (var i = 0; i < Samples; i++)
        {
            var password = SecurePasswordGenerator.Generate();
            Assert.Contains(password, char.IsAsciiLetterUpper);
            Assert.Contains(password, char.IsAsciiDigit);
            Assert.Contains(password, c => !char.IsAsciiLetterOrDigit(c));
            Assert.True(IdentityRules.IsValidUserPassword(password), $"Generated password '{password}' was rejected.");
        }
    }

    [Fact]
    public void Generate_ByDefault_IsTooShortForAnAdministrator()
    {
        Assert.False(IdentityRules.IsValidPassword(SecurePasswordGenerator.Generate()));
    }

    [Fact]
    public void Generate_AvoidsLookAlikeCharactersAndWhitespace()
    {
        for (var i = 0; i < Samples; i++)
        {
            var password = SecurePasswordGenerator.Generate();
            Assert.DoesNotContain(password, c => "0O1lI".Contains(c) || char.IsWhiteSpace(c));
        }
    }

    [Fact]
    public void Generate_ReturnsDifferentPasswords()
    {
        var passwords = Enumerable.Range(0, Samples).Select(_ => SecurePasswordGenerator.Generate()).ToHashSet();
        Assert.Equal(Samples, passwords.Count);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(24)]
    public void Generate_WithExplicitLength_ReturnsThatLength(int length)
    {
        Assert.Equal(length, SecurePasswordGenerator.Generate(length).Length);
    }
}
