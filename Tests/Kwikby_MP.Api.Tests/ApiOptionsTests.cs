using kwikby_MP.Api.Infrastructure;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Kwikby_MP.Api.Tests;

public sealed class ApiOptionsTests
{
    [Fact]
    public void Development_with_a_missing_key_falls_back_to_the_public_dev_key()
    {
        var options = new ApiOptions { Jwt = new JwtOptions { SigningKey = string.Empty } };

        options.Validate(new FakeEnvironment(Environments.Development));

        Assert.Equal(ApiOptions.DevelopmentSigningKey, options.Jwt.SigningKey);
    }

    [Fact]
    public void Production_with_a_missing_key_fails_fast()
    {
        var options = new ApiOptions { Jwt = new JwtOptions { SigningKey = string.Empty } };

        var exception = Assert.Throws<InvalidOperationException>(
            () => options.Validate(new FakeEnvironment(Environments.Production)));

        Assert.Contains("SigningKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_with_a_short_key_fails_fast()
    {
        var options = new ApiOptions { Jwt = new JwtOptions { SigningKey = "too-short" } };

        Assert.Throws<InvalidOperationException>(
            () => options.Validate(new FakeEnvironment(Environments.Production)));
    }

    [Fact]
    public void Production_with_a_32_byte_key_is_accepted()
    {
        var options = new ApiOptions { Jwt = new JwtOptions { SigningKey = new string('k', 32) } };

        options.Validate(new FakeEnvironment(Environments.Production));

        Assert.Equal(32, options.Jwt.SigningKey.Length);
    }

    [Fact]
    public void Zero_values_are_replaced_with_defaults()
    {
        var options = new ApiOptions
        {
            CommandTimeoutSeconds = 0,
            SlowCommandMs = 0,
            Jwt = new JwtOptions { SigningKey = new string('k', 32), AccessTokenMinutes = 0 }
        };

        options.Validate(new FakeEnvironment(Environments.Production));

        Assert.Equal(30, options.CommandTimeoutSeconds);
        Assert.Equal(1_000, options.SlowCommandMs);
        Assert.Equal(60, options.Jwt.AccessTokenMinutes);
    }

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public FakeEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Kwikby_MP.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
