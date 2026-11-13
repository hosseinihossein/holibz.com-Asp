using AspApp.Models;
using Microsoft.EntityFrameworkCore;

namespace AspApp.Helpers;

public class LibraryManager
{
    readonly Library_DbContext _libDb;
    public LibraryManager(Library_DbContext libDb)
    {
        _libDb = libDb;
    }

    public async Task CreateNewUser(Guid userId)
    {
        Library_User_DbModel libUser = new()
        {
            Id = userId,
        };
        await _libDb.Users.AddAsync(libUser);
    }

    public async Task DeleteUser(Guid userId)
    {
        var user = await _libDb.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return;
        }
        _libDb.Users.Remove(user);
    }
}

