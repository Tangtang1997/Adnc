﻿using Adnc.Core.Shared;
using Adnc.Core.Shared.Entities;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Adnc.Infra.EfCore
{
    public class AdncDbContext : DbContext
    {
        private readonly IOperater _operater;
        private readonly IEntityInfo _entityInfo;
        private readonly UnitOfWorkStatus _unitOfWorkStatus;

        public AdncDbContext([NotNull] DbContextOptions options, IOperater operater, [NotNull] IEntityInfo entityInfo, UnitOfWorkStatus unitOfWorkStatus)
            : base(options)
        {
            _operater = operater;
            _entityInfo = entityInfo;
            _unitOfWorkStatus = unitOfWorkStatus;

            //关闭DbContext默认事务
            Database.AutoTransactionsEnabled = false;
            //关闭查询跟踪
            //ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var changedEntities = this.SetAuditFields();

            //没有自动开启事务的情况下,保证主从表插入，主从表更新开启事务。
            var isManualTransaction = false;
            if (!Database.AutoTransactionsEnabled && !_unitOfWorkStatus.IsStartingUow && changedEntities > 1)
            {
                isManualTransaction = true;
                Database.AutoTransactionsEnabled = true;
            }

            var result = base.SaveChangesAsync(cancellationToken);

            //如果手工开启了自动事务，用完后关闭。
            if (isManualTransaction)
                Database.AutoTransactionsEnabled = false;

            return result;
        }

        private int SetAuditFields()
        {
            var allEntities = ChangeTracker.Entries<Entity>();

            var allBasicAuditEntities = ChangeTracker.Entries<IBasicAuditInfo>().Where(x => x.State == EntityState.Added);
            foreach (var entry in allBasicAuditEntities)
            {
                var entity = entry.Entity;
                {
                    entity.CreateBy = _operater.Id;
                    entity.CreateTime = DateTime.Now;
                }
            }

            var auditFullEntities = ChangeTracker.Entries<IFullAuditInfo>().Where(x => x.State == EntityState.Modified);
            foreach (var entry in auditFullEntities)
            {
                var entity = entry.Entity;
                {
                    entity.ModifyBy = _operater.Id;
                    entity.ModifyTime = DateTime.Now;
                }
            }

            return allEntities.Count();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasCharSet("utf8mb4 ");

            var (Assembly, Types) = _entityInfo.GetEntitiesInfo();

            foreach (var entityType in Types)
            {
                modelBuilder.Entity(entityType);
            }

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly);

            //增加全局软删除筛选器
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
                {
                    entityType.AddSoftDeleteQueryFilter();
                }
            }
            base.OnModelCreating(modelBuilder);
        }

        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.LogTo(Console.WriteLine);
    }
    /// <summary>
    /// https://www.cnblogs.com/willick/p/13358580.html
    /// </summary>
    public static class SoftDeleteQueryExtension
    {
        public static void AddSoftDeleteQueryFilter(
            this IMutableEntityType entityData)
        {
            var methodToCall = typeof(SoftDeleteQueryExtension)
                .GetMethod(nameof(GetSoftDeleteFilter),
                    BindingFlags.NonPublic | BindingFlags.Static)
                ?.MakeGenericMethod(entityData.ClrType);
            if (methodToCall == null) return;
            var filter = methodToCall.Invoke(null, new object[] { });
            entityData.SetQueryFilter((LambdaExpression)filter);
        }

        private static LambdaExpression GetSoftDeleteFilter<TEntity>()
            where TEntity : class, ISoftDelete
        {
            Expression<Func<TEntity, bool>> filter = x => !x.IsDeleted;
            return filter;
        }
    }
}