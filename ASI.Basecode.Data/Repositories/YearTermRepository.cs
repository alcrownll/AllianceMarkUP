using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Repositories
{
    public class YearTermRepository : IYearTermRepository
    {
        private readonly AsiBasecodeDBContext _ctx;
        public YearTermRepository(AsiBasecodeDBContext ctx) => _ctx = ctx;

        public IQueryable<YearTerm> GetYearTerms() => _ctx.YearTerms;

        public YearTerm GetYearTermById(int yearTermId) =>
            _ctx.YearTerms.FirstOrDefault(y => y.YearTermId == yearTermId);

        public YearTerm GetYearTerm(int yearLevel, int termNumber) =>
            _ctx.YearTerms.FirstOrDefault(y => y.YearLevel == yearLevel && y.TermNumber == termNumber);

        public void AddYearTerm(YearTerm yearTerm)
        {
            _ctx.YearTerms.Add(yearTerm);
            _ctx.SaveChanges();
        }

        public void UpdateYearTerm(YearTerm yearTerm)
        {
            _ctx.YearTerms.Update(yearTerm);
            _ctx.SaveChanges();
        }

        public void DeleteYearTerm(int yearTermId)
        {
            var entity = GetYearTermById(yearTermId);
            if (entity == null) return;
            _ctx.YearTerms.Remove(entity);
            _ctx.SaveChanges();
        }
    }
}
