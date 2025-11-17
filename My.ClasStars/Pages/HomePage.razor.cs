using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using SharedTypes;
using Syncfusion.Blazor.Calendars;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System;
using System.Linq;
using My.ClasStars.Resources;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Syncfusion.Blazor.Charts;

namespace My.ClasStars.Pages;

public partial class HomePage
{
    #region local variables
    private bool isdisplay;
    private string userid;
    private string School;
    private string[] SchoolCode = Array.Empty<string>();
    private List<RoleInfo> Rolelist;
    private string _email;
    private string _provider;
    private string _Name;
    private bool showDatePicker;
    private bool showDateRangePicker;
    private string TeacherName;
    private string OutOfRangeMessage;
    private int _organizationId = 0;
    private bool isMultipleSchool = false;
    private string userType = "";


    private DateTime StartDate { get; set; } = new(2002, 10, 10);
    private DateTime EndDate { get; set; }= new(2200, 10, 17);
    private DateTime validatestartDate;
    private DateTime validateendDate;
    public static bool IsAdmin;
    private bool IsColorChartVisible;

    private int[] selectedteacher = Array.Empty<int>();
    private int[] selectedschool = Array.Empty<int>();
    private int[] responseEmpIDs = Array.Empty<int>();
    private List<EmployeeInfoDetailed> TeachersList = new List<EmployeeInfoDetailed>();
    private List<RegisteredOrganizationInfo> SchoolList = new List<RegisteredOrganizationInfo>();
    private GetUsageOverviewResponse getUsageOverviewResponse;
    private LoginResultUser loginInfo;
    private List<ExternalProviders> CookieProvidersList = new();
    private DailyActivityCounts DAC = new();
    private DailyActivityCounts resultLineChart = new();

    //For Chart Display
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class Statistics
    {
        public string Name { get; set; }
        public double Percentage { get; set; }
        public string color { get; set; }
    }

    private List<ActivityDailyCountPart2> positiveEvents;
    private List<ActivityDailyCountPart2> negativeEvents;
    private List<ActivityDailyCountPart2> academicEvents;
    private List<ActivityDailyCountPart2> academicNegativeEvents;

    private List<Statistics> PosNegStatisticsDetails;
    private List<Statistics> AttendStatisticsDetails;
    private List<Statistics> ColorChartDetails;
    private bool VisibleProperty { get; set; }
    private bool DialogVisibleProperty { get; set; }
    private bool Ischanged = true;

    #endregion

    protected override async Task OnInitializedAsync()
    {
        VisibleProperty = true;
        if (SchoolServices.Initialized)
        {
            var uri = NavManager.ToAbsoluteUri(NavManager.Uri);
            var query = QueryHelpers.ParseQuery(uri.Query);
            var rc = query.TryGetValue("email", out var queryVal);
            if (rc)
                _email = queryVal.ToString();
            SchoolServices.Email = _email;
            rc = query.TryGetValue("provider", out queryVal);
            if (rc)
                _provider = queryVal.ToString();
            rc = query.TryGetValue("orgId", out queryVal);
            if (rc)
                _organizationId = Convert.ToInt16(queryVal);

            SchoolServices.Provider = _provider;
            rc = query.TryGetValue("given_name", out queryVal);
            if (rc)
                _Name = queryVal.ToString();
            SchoolServices.Name = _Name;
            InvokeServices.SetApplicationInfo(new Version(1, 1), "Classtars Web", "web", "browser");
            var authService = new AuthorizationService(InvokeServices);
            var result = await authService.CheckUserAuthorized(_email, _provider, true, autoCreateUser: false, organizationId: _organizationId);
            School = result.SchoolName;
            if (result.SchoolChoiceList != null)
            {
                if (result.HasMultipleAssociatedSchools)
                {
                    SchoolCode = new string[result.SchoolChoiceList.Count];
                    var data = result.SchoolChoiceList;
                    SchoolList = data;
                    SchoolServices.SchoolList = data;
                    for (int i = 0; i < result.SchoolChoiceList.Count; i++)
                    {
                        SchoolCode[i] = result.SchoolChoiceList[i].OrganizationCode;
                    }
                    NavManager.NavigateTo("/selectSchool");
                }
            }

            if (result.IsAUser && result.OrganizationCode != null)
            {
                var userAuth = new UserAuth(result.OrganizationCode, _email, "");
                loginInfo = await authService.LoginAsync(userAuth);
                if (loginInfo != null)
                {
                    await SetLoginInformation(loginInfo);
                }
            }
        }
        else
        {
            _email = SchoolServices.Email;
            _Name = SchoolServices.Name;
            _provider = SchoolServices.Provider;

            School = SchoolServices.SelectedSchool.Name.ToString();

            var id = SchoolServices.SelectedSchool?.ID ?? 0;
            var authService = new AuthorizationService(InvokeServices);
            var result = await authService.CheckUserAuthorized(_email, _provider, true, id);
            if (SchoolServices.SelectedSchool.ChildOrganizations.Count > 0)
            {
                SchoolList = SchoolServices.SelectedSchool.ChildOrganizations;
                selectedschool = new int[SchoolList.Count];
                //selectedschool[0] = "Select School";
                for (var i = 0; i < SchoolList.Count; i++)
                {
                    selectedschool[i] = SchoolList[i].ID;
                }
                DefaultSchool = result.SelectedSchool.Name.ToString();
            }
            else
            {
                School = result.SchoolName;
                DefaultSchool = School;
            }
            if (result.IsAUser && result.OrganizationCode != null)
            {
                var userAuth = new UserAuth(result.OrganizationCode, _email, "");
                loginInfo = await authService.LoginAsync(userAuth);
                if (loginInfo != null)
                {
                    await SetLoginInformation(loginInfo);
                }
            }
        }
        await InvokeAsync(StateHasChanged);
        await UpdateCookies();
    }
    string DefaultSchool = "";
    public bool isCheckedPositive = true;
    public void HandleCheckedChangedPositive(bool newValue)
    {
        isCheckedPositive = newValue;
        StateHasChanged();
    }

    public bool isCheckedNegative = true;
    public void HandleCheckedChangedNegative(bool newValue)
    {
        isCheckedNegative = newValue;
        StateHasChanged();
    }

    public bool isCheckedAcademic = true;
    public void HandleCheckedChangedAcademic(bool newValue)
    {
        isCheckedAcademic = newValue;
        StateHasChanged();
    }

    public bool isCheckedNegativeAcademic = true;
    public void HandleCheckedChangedNegativeAcademic(bool newValue)
    {
        isCheckedNegativeAcademic = newValue;
        StateHasChanged();
    }
    //Updating cookies
    private async Task UpdateCookies()
    {
        try
        {
            var providersJson = await LocalStorage.GetItemAsync<string>("ProvidersList");
            if (!string.IsNullOrEmpty(providersJson))
            {
                CookieProvidersList = JsonConvert.DeserializeObject<List<ExternalProviders>>(providersJson);
            }
            var provider = CookieProvidersList.FirstOrDefault(p => p.Name == _provider);

            if (provider != null)
            {
                provider.LastLoginDate = DateTime.Now;
                provider.ExpiryDate = DateTime.Now.AddYears(1);
            }
            else
            {
                CookieProvidersList.Add(new ExternalProviders { Name = _provider, LastLoginDate = DateTime.Now, ImageUrl = null, ExpiryDate = DateTime.Now.AddYears(1) });
            }
        }
        catch (Exception)
        {
            await LocalStorage.RemoveItemAsync("ProvidersList");
            CookieProvidersList.Add(new ExternalProviders { Name = _provider, LastLoginDate = DateTime.Now, ImageUrl = null, ExpiryDate = DateTime.Now.AddYears(1) });
        }
        var providersJsons = JsonConvert.SerializeObject(CookieProvidersList);
        await LocalStorage.SetItemAsync("ProvidersList", providersJsons);
    }

    private void OkClick()
    {
        OutOfRangeMessage = null;
        DialogVisibleProperty = false;
    }

    void OnTeacherChanged(int[] selectedValues)
    {
        if (selectedValues != null)
        {
            selectedteacher = selectedValues;
            OutOfRangeMessage = null;
            //SearchSummary();
        }
        else
        {
            selectedteacher = new int[] { };
        }
    }
    List<int> matchedEmployeeIds = new();
    void OnSchoolChanged(int[] selectedValues)
    {
        if (selectedValues != null)
        {
            matchedEmployeeIds = TeachersList
            .Where(employee => employee.EmployeeClassInformation
                .Any(info => selectedValues.Contains(info.OrganizationID)))
            .Select(employee => employee.EmployeeID)
            .Distinct()
            .ToList();

            selectedteacher = new int[matchedEmployeeIds.Count];
            for (var i = 0; i < matchedEmployeeIds.Count; i++)
            {
                selectedteacher[i] = matchedEmployeeIds[i];
            }
        }
        else
        {
            selectedteacher = new int[] { };
            selectedschool = new int[] { };
        }
        selectedschool = selectedValues;
        OutOfRangeMessage = null;
    }
    private ElementReference _dateRangePickerRef;

    private async Task OpenDateRangePicker()
    {
        await JSRuntime.InvokeVoidAsync("openDateRangePicker", _dateRangePickerRef);
    }

    private void ToggleDatePicker()
    {
        showDatePicker = !showDatePicker;
        showDateRangePicker = false;
    }

    private async void ToggleDateRangePicker()
    {
        showDateRangePicker = !showDateRangePicker;
        showDatePicker = false;
        if (!showDatePicker)
        {
            await Task.Delay(10);
            await jsRuntime.InvokeVoidAsync("showCalendar");
        }
    }
    private void UpdateEndDate(DateTime selecteddate)
    {
        StartDate = selecteddate;
        EndDate = selecteddate;
    }

    private async void DateValueChangeHandler(RangePickerEventArgs<DateTime> args)
    {
        ToggleDateRangePicker();
        await SearchSummary();
    }

    private async Task SearchSummary()
    {
        if (!selectedteacher.Any())
        {
            OutOfRangeMessage = "Please Select Any User.";
            selectedteacher = new int[] { };
        }
        if (OutOfRangeMessage == null)
        {
            Ischanged = selectedteacher != responseEmpIDs;
            getUsageOverviewResponse = await GetResponse(selectedteacher);
            if (Ischanged)
            {
                StartDate = getUsageOverviewResponse.StartDate;
                EndDate = (getUsageOverviewResponse.EndDate != DateTime.MinValue) ? getUsageOverviewResponse.EndDate : DateTime.MaxValue;
                validatestartDate = StartDate;
                validateendDate = EndDate;
                Ischanged = false;
            }
        }
        else
        {
            getUsageOverviewResponse = null;
            DialogVisibleProperty = true;
            await Getchart(getUsageOverviewResponse);
        }   
    }

    private async Task SetLoginInformation(LoginResultUser user)
    {
        userType = "";
        userid = user.UserId;
        selectedteacher = new int[1];
        if (user.Roles != null)
        {
            AppInfo.AdminUser = AppInfo.UserInfo.Roles.Any(r => r.IsAdministratorRole()) == true;
            AppInfo.PowerUser = AppInfo.UserInfo.Organization.IsEnterpriseLicensed == false || AppInfo.AdminUser;
            IsAdmin = AppInfo.PowerUser;
            //IsAdmin = false;
            Rolelist = user.Roles;
            if (Rolelist != null)
            {
                userType = Rolelist[0].RoleName;
            }
        }
        if (user.LoggedInEmployee != null)
        {
            TeacherName = user.LoggedInEmployee.EmployeeName;
            if (IsAdmin)
            {
                //Getting Teachers list
                TeachersList = await ClasStarsServices.GetTeachersInformation();
                TeachersList.RemoveAll(t => t.EmployeeClassInformation.Count == 0);
                selectedteacher = new int[TeachersList.Count];
                for (var i = 0; i < TeachersList.Count; i++)
                {
                    selectedteacher[i] = TeachersList[i].EmployeeID;
                }
            }
            else
            {
                selectedteacher[0] = user.LoggedInEmployee.EmployeeID;
            }
        }
        getUsageOverviewResponse = await GetResponse(selectedteacher);
        StartDate = (getUsageOverviewResponse?.StartDate == DateTime.MinValue) ? DateTime.Now.AddDays(-7) : (DateTime)getUsageOverviewResponse?.StartDate;
        EndDate = (getUsageOverviewResponse?.EndDate == DateTime.MinValue) ? DateTime.Now : (DateTime)getUsageOverviewResponse?.EndDate;
        //EndDate = getUsageOverviewResponse?.EndDate ?? DateTime.Now;
        validatestartDate = StartDate;
        validateendDate = EndDate;
        isdisplay = true;
        SchoolServices.NavDisplay = true;
        SchoolServices.SelectedSchool = user.Organization;
        if (user.Organization.IsSchool == false)
        {
            SchoolServices.SchoolList = user.Organization.ChildOrganizations;
            SchoolList = user.Organization.ChildOrganizations;
        }
        else
        {
            DefaultSchool = user.Organization.Name;
        }
        //EmployeeInfoDetailed dd = new EmployeeInfoDetailed();
        //dd.EmployeeID = 112;
        //dd.EmployeeName = "AAA";
        //TeachersList.Add(dd);
    }

    private async Task<GetUsageOverviewResponse> GetResponse(int[] teacherslist)
    {
        try
        {
            var request = new GetUsageOverviewRequest
            {
                StartDate = Ischanged ? null : StartDate,
                EndDate = Ischanged ? null : EndDate,
                EmployeeIds = teacherslist,
                AllEmployees = IsAdmin
#if DEBUGX
                    ,
                IncludeDemoClassData = true,
                SchoolIds = selectedschool
#endif
            };
#if DEBUG
            request.IncludeDemoClassData = true;
#else
            request.IncludeDemoClassData = AppInfo.UserInfo.UserId.StartsWith("pnqtester", StringComparison.CurrentCultureIgnoreCase);;
#endif

            var result = await ClasStarsServices.GetSummary(request);
            responseEmpIDs = teacherslist;
            if (result is { TeacherCount: 0, StudentCount: 0 })
            {
                OutOfRangeMessage = "No Record Found..";
                DialogVisibleProperty = true;
            }
            //var requestLineChart = new GetDailyActivityCountsRequest
            //{
            //    OrganizationId = request.OrganizationId,
            //    EmployeeId = request.EmployeeIds.FirstOrDefault(),
            //    StartDate = request.StartDate,
            //    EndDate = request.EndDate
            //};

            // Based on the randaom data
            //resultLineChart = await classtarservice.GetDailyActivityCounts(requestLineChart);

            // Based on the service return
            DAC = result.ActivityCounts;
            if (DAC != null)
            {
                MinVal = GetMaximumCount(DAC);
                positiveEvents = GetLineChartData(DAC.PositiveEvents);
                negativeEvents = GetLineChartData(DAC.NegativeEvents);
                academicEvents = GetLineChartData(DAC.AcademicEvents);
                academicNegativeEvents = GetLineChartData(DAC.AcademicNegativeEvents);
            }
            else
            {
                MinVal = GetMaximumCount(resultLineChart);
                positiveEvents = GetLineChartData(resultLineChart.PositiveEvents);
                negativeEvents = GetLineChartData(resultLineChart.NegativeEvents);
                academicEvents = GetLineChartData(resultLineChart.AcademicEvents);
                academicNegativeEvents = GetLineChartData(resultLineChart.AcademicNegativeEvents);
            }

            await Getchart(result);
            return result;
        }
        catch (Exception)
        {
            OutOfRangeMessage = "Sorry. We experienced an error handling your request.";
            DialogVisibleProperty = true;
        }
        return null;
    }
    int MinVal = 0;
    private int GetMaximumCount(DailyActivityCounts dAC)
    {
        var list = new[] { dAC.PositiveEvents.Count, dAC.NegativeEvents.Count, dAC.AcademicEvents.Count, dAC.AcademicNegativeEvents.Count };
        return list.Max();
    }

    private List<ActivityDailyCountPart2> GetLineChartData(List<ActivityDailyCount> EventsDetail)
    {
        string PreviousDayWas = "";
        string NextDayWouldBe = "";
        int PreviousDateWas = 0;
        int PreviousDay = 0;
        int Month = 0;
        int Year = 0;
        int countPoint = 0;
        List<ActivityDailyCountPart2> Events = new List<ActivityDailyCountPart2>();
        foreach (ActivityDailyCount adc in EventsDetail)
        {
            if (MinVal > 15)
            {
                PreviousDay = adc.ActivityDate.Day;
                Month = adc.ActivityDate.Month;
                Year = adc.ActivityDate.Year;

                if (countPoint > 0)
                {
                    PreviousDayWas = EventsDetail[countPoint - 1].ActivityDate.DayOfWeek.ToString();
                    PreviousDateWas = EventsDetail[countPoint - 1].ActivityDate.Day;
                    if (countPoint < EventsDetail.Count - 1)
                    {
                        NextDayWouldBe = EventsDetail[countPoint + 1].ActivityDate.DayOfWeek.ToString();
                    }
                }
                else if (countPoint == 0)
                {
                    NextDayWouldBe = EventsDetail[countPoint + 1].ActivityDate.DayOfWeek.ToString();
                }
                countPoint++;


                if (adc.ActivityDate.DayOfWeek.ToString() == "Friday")
                {
                    Events.Add(new ActivityDailyCountPart2 { ActivityDate = adc.ActivityDate.DayOfWeek + " [" + adc.ActivityDate.Month + "/" + adc.ActivityDate.Day + "/" + adc.ActivityDate.Year + "]", ActivityCount = adc.ActivityCount });
                    NextDayWouldBe = "";
                }
                else if ((adc.ActivityDate.DayOfWeek.ToString() == "Saturday" || adc.ActivityDate.DayOfWeek.ToString() == "Sunday" || adc.ActivityDate.DayOfWeek.ToString() == "Monday") && (PreviousDayWas == "Thursday" || PreviousDayWas == "Wednesday" || PreviousDayWas == "Tuesday"))
                {
                    int currentDate = 0;
                    int currentMonth = 0;
                    int currentYear = 0;

                    if (Month == 1 || Month == 3 || Month == 5 || Month == 7 || Month == 8 || Month == 10 || Month == 12)
                    {
                        if (PreviousDay == 31)
                        {
                            currentDate = 1;
                            currentMonth = Month + 1;
                            if (Month == 12)
                            {
                                currentYear = Year + 1;
                            }
                            else
                            {
                                currentYear = Year;
                            }
                        }
                        else
                        {
                            if (PreviousDayWas == "Tuesday")
                                currentDate = PreviousDateWas + 3;
                            if (PreviousDayWas == "Wednesday")
                                currentDate = PreviousDateWas + 2;
                            if (PreviousDayWas == "Thursday")
                                currentDate = PreviousDateWas + 1;

                            if (PreviousDay < currentDate)
                            {
                                if (Month == 1)
                                    currentMonth = 12;
                                else
                                    currentMonth = Month - 1;
                            }
                            else
                            {
                                currentMonth = Month;
                            }
                        }
                        currentYear = Year;
                    }
                    else if (Month == 4 || Month == 6 || Month == 9 || Month == 11)
                    {
                        if (PreviousDay == 30)
                        {
                            currentDate = 1;
                            currentMonth = Month + 1;
                        }
                        else
                        {
                            if (PreviousDayWas == "Tuesday")
                                currentDate = PreviousDateWas + 3;
                            if (PreviousDayWas == "Wednesday")
                                currentDate = PreviousDateWas + 2;
                            if (PreviousDayWas == "Thursday")
                                currentDate = PreviousDateWas + 1;

                            //if (PreviousDay < currentDate)
                            //{
                            //    if (Month == 1)
                            //        currentMonth = 12;
                            //    else
                            //        currentMonth = Month - 1;
                            //}
                            //else
                            //{
                            currentMonth = Month;
                            //}

                            currentYear = Year;
                        }
                    }
                    else if (Month == 2)
                    {
                        if (Year % 4 == 0)
                        {
                            if (PreviousDay == 29)
                            {
                                currentDate = 1;
                                currentMonth = Month + 1;
                            }
                            else
                            {
                                if (PreviousDayWas == "Tuesday")
                                {
                                    if (PreviousDateWas == 31)
                                        currentDate = 3;
                                    else if (PreviousDateWas == 30)
                                        currentDate = 2;
                                    else if (PreviousDateWas == 29)
                                        currentDate = 1;
                                    else
                                    {
                                        currentDate = PreviousDateWas + 3;
                                    }
                                }
                                else if (PreviousDayWas == "Wednesday")
                                {
                                    if (PreviousDateWas == 31)
                                        currentDate = 2;
                                    else if (PreviousDateWas == 30)
                                        currentDate = 1;
                                    else
                                    {
                                        currentDate = PreviousDateWas + 2;
                                    }
                                }
                                else if (PreviousDayWas == "Thursday")
                                {
                                    if (PreviousDateWas == 31)
                                        currentDate = 1;
                                    else
                                    {
                                        currentDate = PreviousDateWas + 1;
                                    }
                                }

                                //if (PreviousDay < currentDate)
                                //{
                                //    if (Month == 1)
                                //        currentMonth = 12;
                                //    else
                                //        currentMonth = Month - 1;
                                //}
                                //else
                                //{
                                currentMonth = Month;
                                //}
                            }
                        }
                        else
                        {
                            if (PreviousDay == 28)
                            {
                                currentDate = 1;
                                currentMonth = Month + 1;
                            }
                            else
                            {
                                if (PreviousDayWas == "Tuesday")
                                {
                                    if (PreviousDateWas == 31)
                                        currentDate = 3;
                                    else if (PreviousDateWas == 30)
                                        currentDate = 2;
                                    else if (PreviousDateWas == 29)
                                        currentDate = 1;
                                    else
                                    {
                                        currentDate = PreviousDateWas + 3;
                                    }
                                }
                                else if (PreviousDayWas == "Wednesday")
                                {
                                    if (PreviousDateWas == 31)
                                        currentDate = 2;
                                    else if (PreviousDateWas == 30)
                                        currentDate = 1;
                                    else
                                    {
                                        currentDate = PreviousDateWas + 2;
                                    }
                                }
                                else if (PreviousDayWas == "Thursday")
                                {
                                    if (PreviousDateWas == 31)
                                        currentDate = 1;
                                    else
                                    {
                                        currentDate = PreviousDateWas + 1;
                                    }
                                }
                                //if (PreviousDay < currentDate)
                                //{
                                //    if (Month == 1)
                                //        currentMonth = 12;
                                //    else
                                //        currentMonth = Month - 1;
                                //}
                                //else
                                //{
                                currentMonth = Month;
                                //}
                            }
                        }
                        currentYear = Year;
                    }
                    Events.Add(new ActivityDailyCountPart2 { ActivityDate = "Friday" + " [" + currentMonth + "/" + currentDate + "/" + currentYear + "]", ActivityCount = 0 });
                    PreviousDay = 0;
                    Month = 0;
                    Year = 0;
                    NextDayWouldBe = "";

                }


                //if (adc.ActivityDate.DayOfWeek.ToString() == "Friday")
                //{
                //    Events.Add(new ActivityDailyCountPart2 { ActivityDate = "Friday" + " [" + adc.ActivityDate.Month + "/" + adc.ActivityDate.Day + "/" + adc.ActivityDate.Year + "]", ActivityCount = adc.ActivityCount });
                //}
            }
            else
            {
                Events.Add(new ActivityDailyCountPart2 { ActivityDate = adc.ActivityDate.Month + "/" + adc.ActivityDate.Day, ActivityCount = adc.ActivityCount });
            }
        }
        return Events;
    }

    private async Task Getchart(GetUsageOverviewResponse response)
    {
        if (response != null)
        {
            if (response.PositiveCount != 0 || response.NegativeCount != 0)
            {
                PosNegStatisticsDetails = new List<Statistics>
                {
                    new() { Name = "Positive", Percentage = response.PositiveCount, color = ColorsResource.AppGreenColor },
                    new() { Name = "Negative", Percentage = response.NegativeCount, color = ColorsResource.AppRedColor }
                };
            }
            else
            {
                PosNegStatisticsDetails = null;
            }
            if (response.AbsenceScore != 0 || response.NeedsSupportScore != 0 || response.EngagedScore != 0)
            {
                AttendStatisticsDetails = new List<Statistics>
                {
                    new() { Name = "Absent", Percentage = response.AbsenceScore, color = ColorsResource.AppSkyBlueColor },
                    new() { Name = "Needs Support", Percentage = response.NeedsSupportScore, color = ColorsResource.AppRedColor },
                    new() { Name = "Properly Engaged", Percentage = response.EngagedScore, color = ColorsResource.AppGreenColor }
                };
            }
            else
            {
                AttendStatisticsDetails = null;
            }
            //response.BehaviorStatusExcellentCount = 9;
            if (!response.BehaviorStatusCounts.IsEmpty)
            {
                IsColorChartVisible = true;
                ColorChartDetails = new List<Statistics>
                {
                    new() { Name = "Red", Percentage = response.BehaviorStatusCounts.SevereCount  , color = ColorsResource.AppRedColor },
                    new() { Name = "Orange", Percentage = response.BehaviorStatusCounts.WarningCount, color = ColorsResource.AppOrangeColor },
                    new() { Name = "Yellow", Percentage = response.BehaviorStatusCounts.FairCount, color = ColorsResource.AppYellowColor },
                    new() { Name = "Green", Percentage = response.BehaviorStatusCounts.GoodCount, color = ColorsResource.AppGreenColor },
                    new() { Name = "Blue", Percentage = response.BehaviorStatusCounts.ExcellentCount , color = ColorsResource.AppSkyBlueColor }

                    //new() { Name = "Red", Percentage = 10, color = ColorsResource.AppRedColor },
                    //new() { Name = "Orange", Percentage = 20, color = ColorsResource.AppOrangeColor },
                    //new() { Name = "Yellow", Percentage = 30, color = ColorsResource.AppYellowColor },
                    //new() { Name = "Green", Percentage = 40, color = ColorsResource.AppGreenColor },
                    //new() { Name = "Blue", Percentage = 50, color = ColorsResource.AppSkyBlueColor }
                };
            }
            else
            {
                IsColorChartVisible = false;
            }
        }
        else
        {
            AttendStatisticsDetails = null;
            PosNegStatisticsDetails = null;
            ColorChartDetails = null;
        }
        await InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (getUsageOverviewResponse != null)
        {
            await Getchart(getUsageOverviewResponse);
        }
    }
}

public static class GlobalChart
{
    public static SfChart Chart { get; set; }
}
public class ActivityDailyCountPart2
{
    public string ActivityDate { get; set; }
    public int ActivityCount { get; set; }
}
