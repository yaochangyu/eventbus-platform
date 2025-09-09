using EventBus.Testing.Common;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Reqnroll.BoDi;

namespace EventBus.Platform.Task.IntegrationTest.Hooks;

[Binding]
public class TestHooks
{
    [BeforeTestRun]
    public static void BeforeTestRun()
    {
        // 測試執行前的全域設定
    }

    [BeforeScenario]
    public async System.Threading.Tasks.Task BeforeScenario(IObjectContainer container)
    {
        // 為每個情境建立新的 fixture 實例
        var fixture = new SimpleTaskTestFixture();
        await fixture.InitializeAsync();
        
        // 將 fixture 註冊到容器中
        container.RegisterInstanceAs(fixture);
        
        // 從 fixture 取得所需的服務
        container.RegisterInstanceAs(fixture.ServiceProvider.GetRequiredService<TaskTestContext>());
        container.RegisterInstanceAs(fixture.ApiClient);
        container.RegisterInstanceAs(fixture.Configuration);
    }

    [AfterScenario]
    public async System.Threading.Tasks.Task AfterScenario(IObjectContainer container)
    {
        // 清理 fixture
        if (container.IsRegistered<SimpleTaskTestFixture>())
        {
            var fixture = container.Resolve<SimpleTaskTestFixture>();
            await fixture.DisposeAsync();
        }
    }
}