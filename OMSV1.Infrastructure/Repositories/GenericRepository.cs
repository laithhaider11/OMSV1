﻿using Microsoft.EntityFrameworkCore;
using OMSV1.Domain.SeedWork;
using OMSV1.Domain.Specifications;
using OMSV1.Infrastructure.Persistence;
using System.Linq.Expressions;

namespace OMSV1.Infrastructure.Repositories
{
    public class GenericRepository<T>(AppDbContext context) : IGenericRepository<T> where T : Entity
    {
    private readonly AppDbContext _context = context;
    private readonly DbSet<T> _dbSet = context.Set<T>(); 

        public async Task<IReadOnlyList<T>> ListAllAsync()
        {
            return await _dbSet.ToListAsync();
        }
        private IQueryable<T> ApplySpecification(ISpecification<T> spec)
        {
            return SpecificationEvaluator<T>.GetQuery(_dbSet, spec);
        }
        public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec)
        {
            return await ApplySpecification(spec).ToListAsync();
        }

        public async Task<T> SingleOrDefaultAsync(ISpecification<T> spec)
        {
            return await ApplySpecification(spec).FirstOrDefaultAsync();
        }
   

        public async Task<int> CountAsync(ISpecification<T> spec)
        {
            return await ApplySpecification(spec).CountAsync();
        }

        
        public async Task<T> AddAsync(T entity)
        {
            var addedEntity = await _context.Set<T>().AddAsync(entity);
            
            return addedEntity.Entity;
        }

        public async Task DeleteAsync(T entity)
        {
            _context.Set<T>().Remove(entity);
           
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            return await _context.Set<T>().FindAsync(id); // Lazy loading
        }

        public async Task<IReadOnlyList<T>> GetAllAsync()
        {
            return await _context.Set<T>().ToListAsync();
        }

        public async Task UpdateAsync(T entity)
        {
            _context.Set<T>().Update(entity);
            
        }
        //Profile by Id
        public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
            {
                return await _context.Set<T>().FirstOrDefaultAsync(predicate);
            }


        // Eager loading method with optional includes
       public async Task<T?> GetByIdWithIncludesAsync(int id, params Expression<Func<T, object>>[] includes)
            {
                IQueryable<T> query = _context.Set<T>();

                // Apply includes for eager loading
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }

                return await query.FirstOrDefaultAsync(e => e.Id == id);
            }




        // private IQueryable<T> ApplySpecification(ISpecification<T> spec)
        //     {
        //         var query = _context.Set<T>().AsQueryable();

        //         if (spec.Criteria != null)
        //             query = query.Where(spec.Criteria);

        //         if (spec.Includes != null)
        //             query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));

        //         return query;
        //     }

    }


    
}


