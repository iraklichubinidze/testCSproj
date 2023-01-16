using System.Security.Cryptography;
using System.DirectoryServices.AccountManagement;
using System.Net.NetworkInformation;
using System.Security.Principal;

namespace NotificationChecker;

static class UserIdInfo
{
    public static string GetUserId()
    {
#if DEBUG
        return "36b5ce48-d61b-01c4-bda4-b4e68f27262c";
#endif
        
        var domain = IPGlobalProperties.GetIPGlobalProperties().DomainName;

        if (!string.IsNullOrEmpty(domain))
        {
            var userPrincipal = UserPrincipal.Current;

            if (userPrincipal.Guid.HasValue)
            {
                return userPrincipal.Guid.Value.ToString("D");
            } 
        }
        
        var identity = WindowsIdentity.GetCurrent();
        
        var user = $"{Environment.MachineName}\\{Environment.UserName} {identity.User?.Value}";
        using var hasher = MD5.Create();
        var inputBytes = System.Text.Encoding.ASCII.GetBytes(user);
        var hashBytes = hasher.ComputeHash(inputBytes);
        
        return new Guid(hashBytes).ToString("D");
    }
}