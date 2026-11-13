using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AspApp.Helpers;
using AspApp.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using OpenIddict.Client;
using OpenIddict.Client.AspNetCore;

namespace AspApp;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);


        //******************* Kestrel *******************
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Any, 80);
            options.Limits.MaxRequestBodySize = 16 * 1024;// 16 KB
        });


        //******************* Identity_DbContext *******************
        builder.Services.AddDbContext<Identity_DbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration["Postgres:ConnectionString:Identity"]);
        });
        //******************* OpenIddict_DbContext *******************
        builder.Services.AddDbContext<OpenIddict_DbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration["Postgres:ConnectionString:OpenIddict"]);
            options.UseOpenIddict<Guid>();
        });
        //******************* DataProtection_DbContext *******************
        builder.Services.AddDbContext<DataProtection_DbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration["Postgres:ConnectionString:DataProtection"]);
        });
        //******************* Library_DbContext *******************
        builder.Services.AddDbContext<Library_DbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration["Postgres:ConnectionString:Library"]);
        });


        //******************* Identity service *******************
        builder.Services.AddIdentity<IdentityUser<Guid>, IdentityRole<Guid>>(options =>
        {
            options.User.AllowedUserNameCharacters =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._";

            options.User.RequireUniqueEmail = true;

            options.SignIn.RequireConfirmedEmail = false;

            options.Password.RequireDigit = false;
            options.Password.RequiredLength = 8;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredUniqueChars = 1;
        })
        .AddEntityFrameworkStores<Identity_DbContext>()
        .AddDefaultTokenProviders();


        //******************* identity cookie *******************
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true; // prevent from js access
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // https only
            options.Cookie.SameSite = SameSiteMode.Lax;// protect unsafe methods from csrf
            options.Cookie.Path = "/";

            //Default for "Remember Me". Without "Remember Me" → Session cookie (expires when browser closes)
            options.ExpireTimeSpan = TimeSpan.FromDays(14);//null or don't set for session lifetime
            options.SlidingExpiration = true;//Reset expiration when user interacts halfway through the lifespan

            options.LoginPath = "/Identity/Login";
            //options.AccessDeniedPath = "/Account/AccessDenied";
        });


        //******************* Data Protection *******************
        // Configure shared Data Protection keys in SQL DB
        builder.Services.AddDataProtection()
        .PersistKeysToDbContext<DataProtection_DbContext>() // Store keys in the same DB
        .SetApplicationName("SharedIdentityApp"); // Must match across all servers


        //******************* SecurityStampValidatorOptions *******************
        builder.Services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval = TimeSpan.Zero;
        });


        // **********    CertManager    **********
        CertManager certManager = new(builder.Environment);
        builder.Services.AddSingleton<CertManager>(certManager);

        X509Certificate2 oidcClientEncryptionCert = await certManager.GetRsaKeyEncryptionCert();
        //******************* OpenIddict *******************
        builder.Services.AddOpenIddict()
        .AddCore(options =>
        {
            options.UseEntityFrameworkCore()
            .UseDbContext<OpenIddict_DbContext>()
            .ReplaceDefaultEntities<Guid>();
        })
        .AddClient(options =>//must not be defined as async
        {
            // Note: this sample uses the code flow, but you can enable the other flows if necessary.
            options.AllowAuthorizationCodeFlow();

            // Register the signing and encryption credentials used to protect
            // sensitive data like the state tokens produced by OpenIddict.
            options.AddEncryptionCertificate(oidcClientEncryptionCert)
            .AddSigningKey(new ECDsaSecurityKey(ECDsa.Create(ECCurve.NamedCurves.nistP256)));

            // Register the ASP.NET Core host and configure the ASP.NET Core-specific options.
            options.UseAspNetCore()
            .EnableRedirectionEndpointPassthrough()
            .EnableStatusCodePagesIntegration();

            // Register the System.Net.Http integration and use the identity of the current
            // assembly as a more specific user agent, which can be useful when dealing with
            // providers that use the user agent as a way to throttle requests (e.g Reddit).
            options.UseSystemNetHttp()
            .SetProductInformation(typeof(Program).Assembly);

            // Register the Web providers integrations.
            //
            // Note: to mitigate mix-up attacks, it's recommended to use a unique redirection endpoint
            // URI per provider, unless all the registered providers support returning a special "iss"
            // parameter containing their URL as part of authorization responses. For more information,
            // see https://datatracker.ietf.org/doc/html/draft-ietf-oauth-security-topics#section-4.4.
            options.UseWebProviders()
            .AddGitHub(opts =>
            {
                opts.SetClientId(builder.Configuration["GitHubClientId"]!)
                .SetClientSecret(builder.Configuration["GitHubClientSecret"]!)
                .SetRedirectUri("api/identity/GitHubLoginCallback");
            })
            .AddGoogle(opts =>
            {
                opts.SetClientId(builder.Configuration["GoogleClientId"]!)
                .SetClientSecret(builder.Configuration["GoogleClientSecret"]!)
                .SetRedirectUri("api/identity/GoogleLoginCallback");
            });

            //unComment this if new configs doesn't work
            //options.RemoveEventHandler(OpenIddictClientHandlers.ValidateIssuerParameter.Descriptor);

            // don't unComment this
            /*options.AddRegistration(new OpenIddictClientRegistration
            {
                ProviderName = "GitHub_Provider",
                Issuer = new Uri("https://github.com/login/oauth",UriKind.Absolute),
                ClientId = builder.Configuration["GitHubClientId"],
                ClientSecret = builder.Configuration["GitHubClientSecret"],
                RedirectUri = new Uri("Api/Authentication/GitHubLoginCallback", UriKind.Relative),

                Scopes = {OpenIddictConstants.Scopes.OpenId, "read:user", "user:email"},

                Configuration = new OpenIddictConfiguration
                {
                    AuthorizationResponseIssParameterSupported = true,
                    Issuer = new Uri("https://github.com/login/oauth",UriKind.Absolute),
                    AuthorizationEndpoint = new Uri("https://github.com/login/oauth/authorize",UriKind.Absolute),
                    TokenEndpoint = new Uri("https://github.com/login/oauth/access_token",UriKind.Absolute),
                    JsonWebKeySetUri = new Uri("https://github.com/login/oauth/.well-known/jwks",UriKind.Absolute),

                    ResponseTypesSupported = { OpenIddictConstants.ResponseTypes.Code},
                    ResponseModesSupported = {OpenIddictConstants.ResponseModes.Query},
                    GrantTypesSupported = {OpenIddictConstants.GrantTypes.AuthorizationCode},

                    TokenEndpointAuthMethodsSupported = 
                    {
                        //OpenIddictConstants.ClientAuthenticationMethods.ClientSecretBasic,
                        OpenIddictConstants.ClientAuthenticationMethods.ClientSecretPost,
                    }
                }
            });*/
        });



        //******************* Controllers *******************
        builder.Services.AddControllersWithViews();


        //******************* AntiForgery *******************
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            //options.Cookie.Name = "XSRF-TOKEN";//swap error
        });


        //**************************** MongoDb **************************
        // Register IMongoClient as a Singleton factory
        builder.Services.AddSingleton<IMongoClient>(sp =>
            new MongoClient(builder.Configuration["MongoDb:ConnectionString"])
        );
        // Configs
        builder.Services.AddSingleton<MongoDbConfigs>(sp => new MongoDbConfigs(
            builder.Configuration.GetValue<string>("MongoDb:ConnectionString")!,
            builder.Configuration.GetValue<string>("MongoDb:UserAccountDb")!,
            builder.Configuration.GetValue<string>("MongoDb:ProfileCollection")!,
            builder.Configuration.GetValue<string>("MongoDb:LibraryDb")!,
            builder.Configuration.GetValue<string>("MongoDb:LibraryCollection")!
        ));


        //**************************** Email Sender **************************
        //builder.Services.AddSingleton<NatsEmailSender>();
        builder.Services.AddSingleton<IEmailSender, EmailSender>();


        //******************* IHttpClientFactory *******************
        builder.Services.AddHttpClient();


        //**************************** Turnstile **************************
        builder.Services.AddSingleton<TurnstileService>();
        builder.Services.AddSingleton<TurnstileConfigs>(sp => new TurnstileConfigs(
            builder.Configuration.GetValue<string>("Turnstile:SecretKey")!,
            builder.Configuration.GetValue<bool>("Turnstile:Enabled")
        ));


        //******************* Username Generator *******************
        builder.Services.AddSingleton<UsernameGenerator>();


        // ********** Config Forward Headers **********
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

            // Clear out the default networks (which only trust localhost by default)
            // Because Nginx is handling security filters, we clear this so it accepts the headers passed from your Nginx container.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });


        //******************* Main Configs *******************
        builder.Services.AddSingleton<MainConfigs>((sp) => new MainConfigs(builder.Configuration["MainDomainName"] ?? "https://holibz.com"));
        //******************* Storage Manager *******************
        builder.Services.AddSingleton<StorageManager>();
        //******************* User Profile Manager *******************
        builder.Services.AddSingleton<UserProfileManager>();
        //******************* Library Manager *******************
        builder.Services.AddSingleton<LibraryManager>();


        var app = builder.Build();


        // ********** Initialize IdentityDb **********
        await using (var scope = app.Services.CreateAsyncScope())
        {
            // ***** identityDb *****
            var identityDb = scope.ServiceProvider.GetRequiredService<Identity_DbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var userProfileManager = scope.ServiceProvider.GetRequiredService<UserProfileManager>();
            var libraryManager = scope.ServiceProvider.GetRequiredService<LibraryManager>();
            await IdentityDb_Initializer.StartInitializing(identityDb, userManager, roleManager, app.Configuration, userProfileManager, libraryManager);

            // ***** dataProtectionDb *****
            var dataProtectionDb = scope.ServiceProvider.GetRequiredService<DataProtection_DbContext>();
            await dataProtectionDb.Database.MigrateAsync();

            // ***** openIddictDb *****
            var openIddictDb = scope.ServiceProvider.GetRequiredService<OpenIddict_DbContext>();
            await openIddictDb.Database.MigrateAsync();

            // ***** libraryDb *****
            var libraryDb = scope.ServiceProvider.GetRequiredService<Library_DbContext>();
            await libraryDb.Database.MigrateAsync();
        }


        // ********** User Forward Headers **********
        app.UseForwardedHeaders();


        app.UseRouting();

        //app.UseAntiforgery();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapDefaultControllerRoute();


        app.MapGet("/api/GetIdentityUsers", async (UserManager<IdentityUser<Guid>> userManager) =>
        {
            var users = await userManager.Users
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.EmailConfirmed,
                u.UserName
            })
            .ToArrayAsync();

            return new { identityUsers = users };
        });

        app.MapGet("/api/GetLibraryUsers", async (Library_DbContext libDb) =>
        {
            var users = await libDb.Users
            .Select(u => new
            {
                u.Id,
            })
            .ToArrayAsync();

            return new { libraryUsers = users };
        });

        app.MapGet("/api/GetProfileUsers",
        async (
            IMongoClient mongoClient,
            MongoDbConfigs mongoDbConfigs,
            UserProfileManager profileManager
        ) =>
        {
            IMongoDatabase userAccountDb = mongoClient.GetDatabase(mongoDbConfigs.UserAccountDb);
            IMongoCollection<BsonDocument> mongoCollection = userAccountDb.GetCollection<BsonDocument>(mongoDbConfigs.ProfileCollection);

            IFindFluent<BsonDocument, BsonDocument> findFluent = mongoCollection.Find(Builders<BsonDocument>.Filter.Empty);
            var userListBson = await findFluent.ToListAsync();
            List<Dictionary<string, object?>> userListJson = [];
            foreach (var ub in userListBson)
            {
                userListJson.Add(profileManager.BsonToJson(ub));
            }

            return new { profileUsers = userListJson.ToArray() };
        });

        app.MapGet("/api/DeleteUserByUsername/{username}",
        async (
            string username,
            UserManager<IdentityUser<Guid>> userManager,
            UserProfileManager profileManager,
            LibraryManager libManager
        ) =>
        {
            var user = await userManager.FindByNameAsync(username);
            if (user is not null)
            {
                await userManager.DeleteAsync(user);
                await profileManager.DeleteUserProfile(user.Id);
                await libManager.DeleteUser(user.Id);
                return new { success = true };
            }
            return new { success = false };
        });

        app.MapGet("/api/DeleteAllLibraries",
        async (
            LibraryManager libManager,
            IMongoClient mongoClient,
            MongoDbConfigs mongoDbConfigs,
            UserProfileManager profileManager
        ) =>
        {
            IMongoDatabase userAccountDb = mongoClient.GetDatabase(mongoDbConfigs.UserAccountDb);
            IMongoCollection<BsonDocument> mongoCollection = userAccountDb.GetCollection<BsonDocument>(mongoDbConfigs.LibraryCollection);

            await mongoCollection.DeleteManyAsync(Builders<BsonDocument>.Filter.Empty);

            await libManager.DeleteAllLibraries();

            return new { success = true };
        });


        //******************* app.Run ******************
        Console.WriteLine($"\n*** App is running on all network port '80'");
        app.Run();
    }
}
