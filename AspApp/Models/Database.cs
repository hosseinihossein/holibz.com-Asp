using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NATS.Client.Core;

namespace AspApp.Models;

//*************** DataProtection_DbContext ****************
public class DataProtection_DbContext : DbContext, IDataProtectionKeyContext
{
    public DataProtection_DbContext(DbContextOptions<DataProtection_DbContext> options)
    : base(options) { }

    // This maps to the table that stores keys
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
}

//*************** Identity_DbContext ****************
public class Identity_DbContext : IdentityDbContext<IdentityUser<Guid>, IdentityRole<Guid>, Guid>
{
    public Identity_DbContext(DbContextOptions<Identity_DbContext> options) : base(options) { }
}

//*************** Identity_DbContext ****************
public class Database_Initializer : IHostedService
{
    private readonly DataProtection_DbContext _dataProtectionDb;
    private readonly Identity_DbContext _identityDb;
    private readonly UserManager<IdentityUser<Guid>> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly INatsClient _natsClient;
    private readonly NatsSubjects _natsSubjects;
    private readonly ILogger<Database_Initializer> _logger;
    private readonly string _adminUserName;
    private readonly string _adminPassword;
    private readonly string _adminEmail;
    private readonly string _adminRoleName;

    public Database_Initializer(
        DataProtection_DbContext dataProtectionDb,
        Identity_DbContext identityDb,
        UserManager<IdentityUser<Guid>> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        INatsClient natsClient,
        IOptions<NatsSubjects> natsSubjectOptions,
        ILogger<Database_Initializer> logger,
        IConfiguration configuration
    )
    {
        _dataProtectionDb = dataProtectionDb;
        _identityDb = identityDb;
        _userManager = userManager;
        _roleManager = roleManager;
        _natsClient = natsClient;
        _natsSubjects = natsSubjectOptions.Value;
        _logger = logger;
        _adminEmail = configuration["Identity:AdminEmail"]!;
        _adminUserName = configuration["Identity:AdminUserName"]!;
        _adminPassword = configuration["Identity:AdminPassword"]!;
        _adminRoleName = configuration["Identity:AdminRoleName"]!;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _dataProtectionDb.Database.MigrateAsync();
        await _identityDb.Database.MigrateAsync();

        //***** Create "admin" Identity *****
        IdentityUser<Guid>? admin = await _userManager.FindByNameAsync("admin");
        if (admin == null)
        {
            string adminPassword = _adminPassword;
            admin = new IdentityUser<Guid>
            {
                UserName = _adminUserName,
                Email = _adminEmail,
                EmailConfirmed = true,
            };
            IdentityResult result = await _userManager.CreateAsync(admin, adminPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    //Console.WriteLine(error.Description);
                    _logger.LogWarning(error.Description);
                }
                return;
            }

            // send admin info through Nats to submit their profile 
            Nats_CreateUserAccount_Payload userAccPayload = new Nats_CreateUserAccount_Payload(
                Id: admin.Id,
                CreatedAt: DateTime.UtcNow,
                RemoteAvatarUrl: null
            );
            await _natsClient.PublishAsync(
                subject: _natsSubjects.CreateUserAccount,
                data: userAccPayload,
                cancellationToken: cancellationToken
            );
        }

        //***** Seed Roles *****
        if (await _roleManager.FindByNameAsync(_adminRoleName) == null)
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(_adminRoleName));
            await _userManager.AddToRoleAsync(admin, _adminRoleName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

/*public class Identity_UserDbModel : IdentityUser<Guid>
{
    [MaxLength(128)]
    public string? DisplayName { get; set; }
    [MaxLength(1024)]
    public string? Description { get; set; }
    public bool PublicEmail { get; set; } = false;
    public byte _imageVersion { get; set; } = 0;
    [NotMapped]
    public int ImageVersion
    {
        get => _imageVersion;
        set => _imageVersion = value > 255 || value < 0 ? (byte)0 : (byte)value;
    }
    public bool HasImage { get; set; } = false;
    [MaxLength(1024)]
    public string? RemoteImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Identity_RoleDbModel : IdentityRole<Guid>
{
    public Identity_RoleDbModel() : base() { }
    public Identity_RoleDbModel(string roleName) : base(roleName) { }
    [MaxLength(128)]
    public string Description { get; set; } = string.Empty;
}*/