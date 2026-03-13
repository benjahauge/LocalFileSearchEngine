using FileSearchEngine.Helpers;
using FileSearchEngine.Services;
using FileSearchEngine.UI;


namespace FileSearchEngine;

public static class Program
{
    static Program()
    {
        SentrySdk.Init(options =>
        {
            // A Sentry Data Source Name (DSN) is required.
            // See https://docs.sentry.io/product/sentry-basics/dsn-explainer/
            // You can set it in the SENTRY_DSN environment variable, or you can set it in code here.
            options.Dsn = "https://8cabc4a4c1a7d56941cf3b3b133075ff@o4510443959353344.ingest.de.sentry.io/4511037436526672";

            // When debug is enabled, the Sentry client will emit detailed debugging information to the console.
            // This might be helpful, or might interfere with the normal operation of your application.
            // We enable it here for demonstration purposes when first trying Sentry.
            // You shouldn't do this in your applications unless you're troubleshooting issues with Sentry.
            options.Debug = false;

            // This option is recommended. It enables Sentry's "Release Health" feature.
            options.AutoSessionTracking = true;
        });
    }

    public static async Task<int> Main(string[] args)
    {
        Global.Config = AppConfig.Load();
        
        await PathHelper.InitializePaths(args, Global.Config);
        Global.FileIndex = await IndexService.LoadOrBuildAsync(Global.Config);
        
        Console.CancelKeyPress += (s, e) =>
        {
            AppConfig.SaveIndex(Global.FileIndex);
        };
        
        while (true)
        {
            RenderService.RenderHeader();
            
            if (Global.IsAddPathMode)
            {
                RenderService.RenderAddPathMode();
            }
            else
            {
                RenderService.RenderSearchMode();
            }
            
            RenderService.RenderResults();

            var key = Console.ReadKey(true);
            
            if (Global.IsAddPathMode)
            {
                await InputHandler.HandleAddPathKey(key, Global.Config, Global.CustomPaths);
            }
            else
            {
                await InputHandler.HandleSearchKey(key, Global.Config);
            }
        }
    }
}
