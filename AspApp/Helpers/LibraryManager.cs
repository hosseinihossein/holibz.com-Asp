using AspApp.Models;

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
}

