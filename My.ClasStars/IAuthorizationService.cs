using System.Threading.Tasks;
using SharedTypes;

namespace My.ClasStars;

public interface IAuthorizationService
{
    Task<string> GetToken();

    Task<LoggedInUserInfo> CheckUserAuthorized(string email, string provider, bool activeUsersOnly,
        int organizationId = 0, bool autoCreateUser = false, bool isEnterpriseUser = false, bool failOnEnterpriseConflict = true);

    Task<LoginResultUser> LoginAsync(UserAuth user);
}