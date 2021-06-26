namespace YoYoStudio
{
    namespace Plugins
    {
        namespace ZplPrePostBuildPlugin
        {
            public class ZplPrePostBuildPluginInit : IPlugin
            {
                public PluginConfig Initialise()
                {
                    PluginConfig cfg = new PluginConfig("PrePostBuild for Zeus", "Ability to define Pre-Build and Post-Build actions for your project.", false);
                    cfg.AddCommand("zplprepostbuildplugin_command", "ide_loaded", "I really wouldn't mind some hugs right now!", "create", typeof(ZplPrePostBuildCommand));
                    return cfg;
                }
            }
        }
    }
}
