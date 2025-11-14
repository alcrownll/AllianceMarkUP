using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class AccountsFilters
    {
        public string? Program { get; set; }
        public string? YearLevel { get; set; }
        public string? Name { get; set; }
        public string? IdNumber { get; set; }
        public string? Status { get; set; }
        public string? Position { get; set; }
    }

    public class StudentListItem
    {
        public int StudentId { get; set; }
        public int UserId { get; set; }
        public string Program { get; set; } = "";
        public string YearLevel { get; set; } = "";
        public string FullName { get; set; } = "";
        public string IdNumber { get; set; } = "";
    }

    public class TeacherListItem
    {
        public int TeacherId { get; set; }
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Position { get; set; } = "";
        public string IdNumber { get; set; } = "";
    }

    public class AccountsFilterResult
    {
        public List<StudentListItem> Students { get; set; } = new();
        public List<TeacherListItem> Teachers { get; set; } = new();
        public List<string> Programs { get; set; } = new();
        public List<string> YearLevels { get; set; } = new();
        public AccountsFilters Filters { get; set; } = new();
    }

    public enum ManageTab { Students, Teachers }

    public class AdminAccountsViewModel
    {
        public ManageTab ActiveTab { get; set; } = ManageTab.Students;
        public AccountsFilters Filters { get; set; } = new();
        public List<StudentListItem> Students { get; set; } = new();
        public List<TeacherListItem> Teachers { get; set; } = new();
        public List<string> Programs { get; set; } = new();
        public List<string> YearLevels { get; set; } = new();
    }
}
