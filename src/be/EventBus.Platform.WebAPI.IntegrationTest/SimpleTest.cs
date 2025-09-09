using EventBus.Testing.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EventBus.Platform.Task.IntegrationTest;

public class SimpleTest : IClassFixture<SimpleTaskTestFixture>
{
    private readonly SimpleTaskTestFixture _fixture;
    
    public SimpleTest(SimpleTaskTestFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public void TestFixtureInitialization()
    {
        // 測試 fixture 是否正確初始化
        _fixture.Should().NotBeNull();
        _fixture.ServiceProvider.Should().NotBeNull();
        _fixture.ApiClient.Should().NotBeNull();
        _fixture.Configuration.Should().NotBeNull();
        
        // 測試服務是否可以正確解析
        var context = _fixture.ServiceProvider.GetRequiredService<TaskTestContext>();
        context.Should().NotBeNull();
        
        var logger = _fixture.ServiceProvider.GetRequiredService<ILogger<SimpleTest>>();
        logger.Should().NotBeNull();
        
        logger.LogInformation("Fixture test passed!");
    }
}