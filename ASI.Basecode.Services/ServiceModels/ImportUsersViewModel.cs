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
        public bool HasErrors => FailedCount > 0 || InsertedCount == 0;
        public bool IsSuccess => InsertedCount > 0 && FailedCount == 0;

  
        public string GetMessage()
        {
            if (InsertedCount == 0 && FailedCount == 0)
            {
                return "The file you uploaded appears to be empty. Please add some data and try uploading again.";
            }

            if (InsertedCount == 0 && FailedCount > 0)
            {

                return SimplifyErrorMessage(FirstError ?? "We couldn't process your file. Please check the data and try again.");
            }

            if (InsertedCount > 0 && FailedCount > 0)
            {
                return $"Good news! We imported {InsertedCount} {(InsertedCount == 1 ? "record" : "records")}, but {FailedCount} {(FailedCount == 1 ? "record" : "records")} couldn't be added. {SimplifyErrorMessage(FirstError)}";
            }

            return $"Success! {InsertedCount} {(InsertedCount == 1 ? "record" : "records")} imported successfully.";
        }

        private string SimplifyErrorMessage(string? error)
        {
            if (string.IsNullOrWhiteSpace(error))
                return "Please check your file and try again.";

            var parts = error.Split(new[] { ": " }, 2, StringSplitOptions.None);
            if (parts.Length > 1)
                return parts[1];

            return error;
        }

        public string GetDetailedError()
        {
            return FirstError ?? "An unknown error occurred.";
        }
    }
}
