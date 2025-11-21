using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Services.Exceptions
{

    public class BaseServiceException : Exception
    {
        public BaseServiceException(string message) : base(message) { }

        public BaseServiceException(string message, Exception innerException)
            : base(message, innerException) { }
    }

 
    public class NotFoundException : BaseServiceException
    {
        public NotFoundException(string entityName, object id)
            : base($"{entityName} with ID '{id}' was not found.") { }

        public NotFoundException(string message)
            : base(message) { }
    }

 
    public class ValidationException : BaseServiceException
    {
        public Dictionary<string, string[]> Errors { get; }

        public ValidationException(string message)
            : base(message)
        {
            Errors = new Dictionary<string, string[]>();
        }

        public ValidationException(Dictionary<string, string[]> errors)
            : base("One or more validation errors occurred.")
        {
            Errors = errors;
        }
    }

    public class DuplicateException : BaseServiceException
    {
        public DuplicateException(string entityName, string fieldName, object value)
            : base($"{entityName} with {fieldName} '{value}' already exists.") { }

        public DuplicateException(string message)
            : base(message) { }
    }


    public class UnauthorizedException : BaseServiceException
    {
        public UnauthorizedException(string message = "You are not authorized to perform this action.")
            : base(message) { }
    }

  
    public class BadRequestException : BaseServiceException
    {
        public BadRequestException(string message)
            : base(message) { }
    }

  
    public class DatabaseException : BaseServiceException
    {
        public DatabaseException(string message)
            : base(message) { }

        public DatabaseException(string message, Exception innerException)
            : base(message, innerException) { }
    }

  
    public class CourseInUseException : BaseServiceException
    {
        public string CourseCode { get; }
        public List<string> ProgramsUsingCourse { get; }

        public CourseInUseException(string courseCode, List<string> programCodes)
            : base(BuildMessage(courseCode, programCodes))
        {
            CourseCode = courseCode;
            ProgramsUsingCourse = programCodes ?? new List<string>();
        }

        private static string BuildMessage(string courseCode, List<string> programCodes)
        {
            if (programCodes == null || programCodes.Count == 0)
            {
                return $"Cannot delete course '{courseCode}' because it is currently assigned to one or more programs.";
            }

            string programList = string.Join(", ", programCodes);
            return $"Cannot delete course '{courseCode}' because it is currently assigned to the following program(s): {programList}. Please remove the course from these programs before deleting.";
        }
    }


    public class CoursePrerequisiteException : BaseServiceException
    {
        public CoursePrerequisiteException(string message)
            : base(message) { }

        public CoursePrerequisiteException(string courseCode, string prerequisiteCode)
            : base($"Cannot perform this operation. Course '{courseCode}' has a prerequisite dependency with '{prerequisiteCode}'.") { }
    }


    public class DuplicateCourseCodeException : BaseServiceException
    {
        public string CourseCode { get; }

        public DuplicateCourseCodeException(string courseCode)
            : base($"A course with code '{courseCode}' already exists.")
        {
            CourseCode = courseCode;
        }
    }

    public class DuplicateProgramException : BaseServiceException
    {
        public string FieldName { get; }
        public string FieldValue { get; }

        public DuplicateProgramException(string fieldName, string fieldValue)
            : base($"A program with the {fieldName} '{fieldValue}' already exists.")
        {
            FieldName = fieldName;
            FieldValue = fieldValue;
        }
    }


}