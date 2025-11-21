using ASI.Basecode.Data.Models;
using System.Linq;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IYearTermRepository
    {
        IQueryable<YearTerm> GetYearTerms();
        YearTerm GetYearTermById(int yearTermId);
        YearTerm GetYearTerm(int yearLevel, int term); 

        void AddYearTerm(YearTerm yearTerm);
        void UpdateYearTerm(YearTerm yearTerm);
        void DeleteYearTerm(int yearTermId);
    }
}
