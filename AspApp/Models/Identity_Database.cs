using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AspApp.Helpers;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
public static class IdentityDb_Initializer
{
    public static async Task StartInitializing(
        Identity_DbContext identityDb,
        UserManager<IdentityUser<Guid>> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IConfiguration configuration,
        UserProfileManager userProfileManager,
        LibraryManager libraryManager
    )
    {
        string adminEmail = configuration["Identity:AdminEmail"]!;
        string adminUserName = configuration["Identity:AdminUserName"]!;
        string adminPassword = configuration["Identity:AdminPassword"]!;
        string adminRoleName = configuration["Identity:AdminRoleName"]!;

        await identityDb.Database.MigrateAsync();

        //***** Create "admin" Identity *****
        IdentityUser<Guid>? admin = await userManager.FindByNameAsync("admin");
        if (admin == null)
        {
            admin = new IdentityUser<Guid>
            {
                UserName = adminUserName,
                Email = adminEmail,
                EmailConfirmed = true,
            };
            IdentityResult result = await userManager.CreateAsync(admin, adminPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    Console.WriteLine(error.Description);
                    //_logger.LogWarning(error.Description);
                }
                return;
            }

            // send new user to profile manager
            NewUserProfileModel newUserAccountModel = new()
            {
                Id = admin.Id,
            };
            await userProfileManager.CreateNewUserProfile(newUserAccountModel);

            // send new user to library manager
            await libraryManager.CreateNewUser(admin.Id);
        }

        //***** Seed Roles *****
        if (await roleManager.FindByNameAsync(adminRoleName) == null)
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(adminRoleName));
            await userManager.AddToRoleAsync(admin, adminRoleName);
        }
    }
}
