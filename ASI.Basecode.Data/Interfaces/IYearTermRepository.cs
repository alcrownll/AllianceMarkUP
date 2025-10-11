using ASI.Basecode.Data.Models;
using System.Linq;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IYearTermRepository
    {
        IQueryable<YearTerm> GetYearTerms();
        YearTerm GetYearTermById(int yearTermId);
        YearTerm GetYearTerm(int yearLevel, int term); // e.g., (1..4, 1..2)

        // Usually YearTerm is seeded/static, so no add/update/delete needed,
        // added just in case :)
        void AddYearTerm(YearTerm yearTerm);
        void UpdateYearTerm(YearTerm yearTerm);
        void DeleteYearTerm(int yearTermId);
    }
}
