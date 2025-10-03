using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAdminCreateAccountService
    {
        (byte[] Content, string ContentType, string FileName) GenerateStudentsTemplate();
        Task<ImportResult> ImportStudentsAsync(IFormFile file, ImportUserDefaults defaults, CancellationToken ct);

        (byte[] Content, string ContentType, string FileName) GenerateTeachersTemplate();
        Task<ImportResult> ImportTeachersAsync(IFormFile file, ImportUserDefaults defaults, CancellationToken ct);
    }
}
