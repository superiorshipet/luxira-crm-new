using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Luxira.Api.Features.Employees.DTOs;

namespace Luxira.Api.IntegrationTests;

public sealed class EmployeeFeatureTests(LuxiraApiFactory factory) : IClassFixture<LuxiraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task EmployeeAndAttendanceFlowSucceeds()
    {
        var token = TestJwtTokenFactory.Create("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createEmp = new CreateEmployeeRequest(
            Name: "حسين الشمري",
            DisplayName: "حسين",
            IdNumber: "987654321",
            Nationality: "عراقي",
            Country: "العراق",
            PhoneNumber: "07800000000",
            Address: "بغداد",
            Salary: 750000m,
            JobTitle: "مجهز طلبات",
            ApplicationUserId: null
        );

        var empResponse = await _client.PostAsJsonAsync("/api/v1/employees", createEmp);
        Assert.Equal(HttpStatusCode.Created, empResponse.StatusCode);

        var emp = await empResponse.Content.ReadFromJsonAsync<EmployeeDto>();
        Assert.NotNull(emp);
        Assert.True(emp.Id > 0);

        // Check in
        var checkInReq = new CheckInRequest(emp.Id, "بدء الدوام الصباحي");
        var checkInResponse = await _client.PostAsJsonAsync("/api/v1/attendance/check-in", checkInReq);
        Assert.Equal(HttpStatusCode.OK, checkInResponse.StatusCode);

        var log = await checkInResponse.Content.ReadFromJsonAsync<AttendanceLogDto>();
        Assert.NotNull(log);

        // Check out
        var checkOutReq = new CheckOutRequest(log.Id, "انتهاء الدوام");
        var checkOutResponse = await _client.PostAsJsonAsync("/api/v1/attendance/check-out", checkOutReq);
        Assert.Equal(HttpStatusCode.OK, checkOutResponse.StatusCode);

        // Salary payment
        var salaryReq = new RecordSalaryPaymentRequest(emp.Id, 750000m, "راتب شهر سبتمبر");
        var salaryResponse = await _client.PostAsJsonAsync("/api/v1/salaries/pay", salaryReq);
        Assert.Equal(HttpStatusCode.OK, salaryResponse.StatusCode);
    }
}
