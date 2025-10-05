using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.Authentication;
using ASI.Basecode.WebApp.Models;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ASI.Basecode.WebApp
{
    // Other services configuration
    internal partial class StartupConfigurer
    {
        /// <summary>
        /// Configures the other services.
        /// </summary>
        private void ConfigureOtherServices()
        {
            // Framework
            this._services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            this._services.TryAddSingleton<IActionContextAccessor, ActionContextAccessor>();

            // Common
            this._services.AddScoped<TokenProvider>();
            this._services.TryAddSingleton<TokenProviderOptionsFactory>();
            this._services.TryAddSingleton<TokenValidationParametersFactory>();
            this._services.AddScoped<IUnitOfWork, UnitOfWork>();

            // SERVICES
            this._services.TryAddSingleton<TokenValidationParametersFactory>();
            this._services.AddScoped<IUserService, UserService>();
            this._services.AddScoped<IAdminAccountsService, AdminAccountsService>();
            this._services.AddScoped<IAdminCreateAccountService, AdminCreateAccountService>();
            this._services.AddScoped<IProfileService, ProfileService>();
            this._services.AddScoped<ICourseService, CourseService>();   
            this._services.AddScoped<ITeacherCourseService, TeacherCourseService>();
            this._services.AddScoped<IRightSidebarService, RightSidebarService>();
            this._services.AddScoped<ICalendarService, CalendarService>();

            // Repositories
            this._services.AddScoped<IUserRepository, UserRepository>();
            this._services.AddScoped<IUserProfileRepository, UserProfileRepository>();
            this._services.AddScoped<IStudentRepository, StudentRepository>();
            this._services.AddScoped<ITeacherRepository, TeacherRepository>();
            this._services.AddScoped<ICourseRepository, CourseRepository>();
            this._services.AddScoped<IAssignedCourseRepository, AssignedCourseRepository>();
            this._services.AddScoped<IClassScheduleRepository, ClassScheduleRepository>();
            this._services.AddScoped<IGradeRepository, GradeRepository>();
            this._services.AddScoped<INotificationRepository, NotificationRepository>();
            this._services.AddScoped<ICalendarEventRepository, CalendarEventRepository>();

            // Manager Class
            this._services.AddScoped<SignInManager>();

            this._services.AddHttpClient();
        }
    }
}
