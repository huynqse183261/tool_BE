using BookfetSystem.Repositories.Basic;
using Microsoft.EntityFrameworkCore;
using Repositories.DBContext;
using Repositories.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories
{
    public class AiGenerationRepository : GenericRepository<AiGeneration>
    {
        public AiGenerationRepository(toolContext context) : base(context)
        {
        }
    }
}