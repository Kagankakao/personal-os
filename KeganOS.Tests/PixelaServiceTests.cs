using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using KeganOS.Infrastructure.Services;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace KeganOS.Tests;

/// <summary>
/// Unit tests for PixelaService
/// </summary>
public class PixelaServiceTests
{
    private readonly PixelaService _sut;
    private readonly User _testUser;

    public PixelaServiceTests()
    {
        _sut = new PixelaService();
        _testUser = new User
        {
            Id = 1,
            DisplayName = "Test User",
            PixelaUsername = "testuser",
            PixelaToken = "testtoken123",
            PixelaGraphId = "graph1"
        };
    }

    [Fact]
    public void IsConfigured_WithValidUser_ReturnsTrue()
    {
        // Act
        var result = _sut.IsConfigured(_testUser);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsConfigured_WithMissingUsername_ReturnsFalse()
    {
        // Arrange
        var user = new User { PixelaToken = "token", PixelaGraphId = "graph1" };

        // Act
        var result = _sut.IsConfigured(user);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsConfigured_WithMissingToken_ReturnsFalse()
    {
        // Arrange
        var user = new User { PixelaUsername = "user", PixelaGraphId = "graph1" };

        // Act
        var result = _sut.IsConfigured(user);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsConfigured_WithMissingGraphId_ReturnsFalse()
    {
        // Arrange
        var user = new User { PixelaUsername = "user", PixelaToken = "token" };

        // Act
        var result = _sut.IsConfigured(user);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GenerateToken_ReturnsConsistentHash()
    {
        // Arrange
        var username = "testuser";

        // Act
        var token1 = _sut.GenerateToken(username);
        var token2 = _sut.GenerateToken(username);

        // Assert
        Assert.Equal(token1, token2);
        Assert.NotEmpty(token1);
    }

    [Fact]
    public void GenerateToken_DifferentUsernames_ReturnDifferentTokens()
    {
        // Arrange
        var username1 = "user1";
        var username2 = "user2";

        // Act
        var token1 = _sut.GenerateToken(username1);
        var token2 = _sut.GenerateToken(username2);

        // Assert
        Assert.NotEqual(token1, token2);
    }

    [Theory]
    [InlineData("validuser", true)]
    [InlineData("UPPERCASE", false)]
    [InlineData("with spaces", false)]
    [InlineData("ab", false)] // too short
    [InlineData("valid123", true)]
    public async Task CheckUsernameAvailability_ValidatesFormat(string username, bool expectedValid)
    {
        // Act
        var (isValid, error) = await _sut.CheckUsernameAvailabilityAsync(username);

        // Assert - we can only check format validation locally
        // Remote availability check may vary
        if (!expectedValid)
        {
            Assert.False(isValid);
            Assert.NotNull(error);
        }
    }
}
