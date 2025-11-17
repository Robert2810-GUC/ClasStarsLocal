using System.Collections.Generic;

namespace My.ClasStars;

public class SchoolListService
{
    public List<SharedTypes.RegisteredOrganizationInfo> SchoolList { get; set; }
    public SharedTypes.RegisteredOrganizationInfo SelectedSchool { get; set; }

    public string Email { get; set; }
    public string Name { get; set; }
    public string Provider { get; set; }

    public bool Initialized { get; set; } = true;

    public bool NavDisplay { get; set; } = true;

}