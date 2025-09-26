using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class ImportUserDefaults
    {
        public string DefaultAccountStatus { get; set; } = "Active";
        public string DefaultStudentStatus { get; set; } = "Enrolled";
        public string DefaultTeacherPosition { get; set; } = "Instructor";
    }

    public class ImportResult
    {
        public int InsertedCount { get; set; }
        public int FailedCount { get; set; }
        public string? FirstError { get; set; }
        public bool HasErrors => FailedCount > 0;
    }
}
