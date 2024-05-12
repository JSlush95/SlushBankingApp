using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(BankingApp.Startup))]
namespace BankingApp
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
