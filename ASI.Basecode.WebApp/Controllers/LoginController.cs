using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

public class LoginController : Controller
{
    [HttpGet]
    [AllowAnonymous] // 👈 allows access without authentication remove this once magstart with bacend and db
    public IActionResult StudentLogin()
    {
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    public IActionResult StudentLogin(string username, string password)
    {
        if (username == "student" && password == "123")
            return RedirectToAction("Dashboard", "Student");

        ViewBag.Error = "Invalid Student login.";
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult TeacherLogin() => View();

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AdminLogin() => View();

    [Authorize(Roles = "Student")]
    public async Task<IActionResult> StudentLogout()
    {
        await HttpContext.SignOutAsync("ASI_Basecode");
        return RedirectToAction("StudentLogin", "Login");
    }

    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> TeacherLogout()
    {
        await HttpContext.SignOutAsync("ASI_Basecode");
        return RedirectToAction("TeacherLogin", "Login");
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminLogout()
    {
        await HttpContext.SignOutAsync("ASI_Basecode");
        return RedirectToAction("AdminLogin", "Login");
    }

}