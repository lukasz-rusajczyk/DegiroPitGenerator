using System.Linq;
using System.Threading.Tasks;
using Integrations.Degiro.Models.Configuration;
using Microsoft.Playwright;

namespace Integrations.Degiro
{
    public class IntegrationFactory
    {
        private readonly DegiroConfiguration _configuration;

        public IntegrationFactory(DegiroConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IIntegration> Create()
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            await page.GotoAsync(_configuration.Login.LoginUrl);

            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            await page.FillAsync($"xpath={_configuration.Login.XPaths.UsernameTextBox}", _configuration.Credentials.Username);
            await page.FillAsync($"xpath={_configuration.Login.XPaths.PasswordTextBox}", _configuration.Credentials.Password);
            await page.ClickAsync($"xpath={_configuration.Login.XPaths.LoginButton}");

            // Wait until network activity settles after login
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var cookies = await context.CookiesAsync();
            var jSessionId = cookies.Single(c => c.Name == _configuration.Login.SessionCookieName).Value;

            return new Integration(_configuration.Requests, jSessionId);
        }
    }
}
