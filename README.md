# BankingApp
This application uses ASP.Net Identity as the base framework with a few customizability-related actions, such as extended other tables to its original database schema and changing the User IDs from string types to integers.

# Web.config-related note:
Implemented an extended appsecrets.config file that will contain the sensitive information for email-related activities. To have this work locally for sending emails, add your own "AppSettingsSecrets.config" file with the valid information like host, username, port, etc. You can also rename the file to whatever you wish, but you must also change the referenced name in the web.config file. Otherwise, the email system will not work. Alternative methods of dependency injection are possible, but I chose this approach due to this project not being ASP.NET CORE, which many other solutions were based in. My approach was influenced by John Atten's [guide](https://johnatten.com/2014/04/06/asp-net-mvc-keep-private-settings-out-of-source-control/) and the Learn Microsoft ASP.NET [documentation](https://learn.microsoft.com/en-us/aspnet/identity/overview/features-api/best-practices-for-deploying-passwords-and-other-sensitive-data-to-aspnet-and-azure).
