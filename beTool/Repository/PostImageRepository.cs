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
    public class PostImageRepository : GenericRepository<PostImage>
    {
        public PostImageRepository(toolContext context) : base(context)
        {
        }
    }
}