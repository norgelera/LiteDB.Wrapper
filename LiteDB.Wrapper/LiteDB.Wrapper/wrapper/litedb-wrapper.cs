﻿using LiteDB.Wrapper.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LiteDB.Wrapper
{
    /// <summary></summary>
    public class CollectionReference<T> : ICollectionRef<T>, IDisposable
    {
        private IList<T> ToSave { get; set; } = new List<T>();
        private IList<T> ToModify { get; set; } = new List<T>();
        private IList<Guid> ToRemove { get; set; } = new List<Guid>();
        private CollectionReferenceConfig RefConfig { get; set; }

        CollectionReferenceConfig ICollectionRef<T>.Config => RefConfig;

        /// <summary></summary>
        public CollectionReference(string location, string collection) => RefConfig = new CollectionReferenceConfig(location, collection);

        /// <summary>Insert an object to the referenced collection.</summary>
        void ICollectionRef<T>.Insert(T obj) => ToSave.Add(obj);
        /// <summary>Insert a list of objects to the referenced collection.</summary>
        void ICollectionRef<T>.Insert(IList<T> objs) => ToSave = ToSave.Concat(objs).ToList();

        /// <summary>Update an object in the referenced collection.</summary>
        void ICollectionRef<T>.Update(T obj) => ToModify.Add(obj);
        /// <summary>Update a list of objects in the referenced collection.</summary>
        void ICollectionRef<T>.Update(IList<T> objs) => ToModify = ToModify.Concat(objs).ToList();

        /// <summary>Remove an object that matches the given id.</summary>
        void ICollectionRef<T>.Remove(Guid id) => ToRemove.Add(id);
        /// <summary>Remove objects that matches the given ids.</summary>
        void ICollectionRef<T>.Remove(IList<Guid> ids) => ToRemove = ToRemove.Concat(ids).ToList();

        /// <summary>Commit changes made to the collection.</summary>
        async Task ICollectionRef<T>.Commit()
        {
            try
            {
                // For some odd reasons, current LiteDB version does not support transaction
                using (LiteRepository _liteRepo = new LiteRepository(RefConfig.Location))
                {
                    if (ToSave.Any() || ToModify.Any())
                    {
                        IList<T> _combinedList = ToSave.Concat(ToModify).ToList();
                        _liteRepo.Upsert<T>(_combinedList, RefConfig.Collection);
                    }
                    if (ToRemove.Any())
                        _liteRepo.Delete<T>(Query.Where("_id", id => ToRemove.Contains(id)), RefConfig.Collection);
                }
                await Task.Run(() =>
                {
                    ToSave.Clear();
                    ToModify.Clear();
                    ToRemove.Clear();
                });
            }
            catch (Exception ex)
            { throw ex; }
        }

        /// <summary>Get an item from the referenced collection.</summary>
        T ICollectionRef<T>.Get(Guid id)
        {
            try
            {
                using (LiteDatabase _liteDB = new LiteDatabase(RefConfig.Location))
                {
                    var _collection = _liteDB.GetCollection<T>(RefConfig.Collection);
                    return _collection.IncludeAll().FindById(id);
                }
            }
            catch (Exception ex)
            { throw ex; }
        }

        /// <summary>Get a paginated list of items from the referenced collection.</summary>
        PagedResult<T> ICollectionRef<T>.GetPaged(PageOptions pageOptions, SortOptions sortOptions)
        {
            try
            {
                using (LiteDatabase _liteDB = new LiteDatabase(RefConfig.Location))
                {
                    var _collection = _liteDB.GetCollection<T>(RefConfig.Collection);
                    _collection.EnsureIndex(sortOptions.Field, true);
                    long _countAll = _liteDB.GetCollection<T>(RefConfig.Collection).Count();
                    return new PagedResult<T>(_countAll, _collection.IncludeAll().Find(Query.All(sortOptions.Field, (int)sortOptions.Sort), pageOptions.Offset, pageOptions.Rows).ToList());
                }
            }
            catch (Exception ex)
            { throw ex; }
        }

        /// <summary>Explicitly drop currenct collection.</summary>
        void ICollectionRef<T>.Drop()
        {
            try
            {
                using (LiteDatabase _liteDb = new LiteDatabase(RefConfig.Location))
                {
                    _liteDb.DropCollection(RefConfig.Collection);
                }
            }
            catch (Exception ex)
            { throw ex; }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        /// <summary></summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing) { }
                disposedValue = true;
            }
        }

        /// <summary></summary>
        public void Dispose() => Dispose(true);
        #endregion
    }
}