using Microsoft.EntityFrameworkCore;

namespace AspApp.Models;

public class OpenIddict_DbContext : DbContext
{
    public OpenIddict_DbContext(DbContextOptions<OpenIddict_DbContext> options)
        : base(options) { }
}