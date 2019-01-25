﻿using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.CodeGeneration;
using Stardust.Paradox.Data.Traversals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data.Internals
{
	internal class EdgeGraphSet<T> : IEdgeGraphSet<T> where T : IEdgeEntity
	{
		private readonly IGraphContext _context;

		internal EdgeGraphSet(IGraphContext context)
		{
			_context = context;
		}

		public async Task<T> GetAsync(string id)
		{
			return await _context.EAsync<T>(id).ConfigureAwait(false);
		}

		public async Task<IEnumerable<T>> FilterAsync(Expression<Func<T, object>> byProperty, string hasValue)
		{
			var propertyName = QueryFuncExt.GetInfo(byProperty).ToCamelCase();
			return await _context.EAsync<T>(g => g.E().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)]).Has(propertyName, hasValue)).ConfigureAwait(false);
		}

		public async Task<IEnumerable<T>> FilterAsync(Expression<Func<T, object>> byProperty, string hasValue, int page, int pageSize = 20)
		{
			var propertyName = QueryFuncExt.GetInfo(byProperty).ToCamelCase();
			return await _context.EAsync<T>(g => g.E().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)]).Has(propertyName, hasValue).SkipTake(page * pageSize, pageSize)).ConfigureAwait(false);
		}

		public async Task<IEnumerable<T>> AllAsync()
		{
			return await _context.EAsync<T>(g => g.E().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)])).ConfigureAwait(false);
		}

		public async Task<IEnumerable<T>> AllAsync(int page, int pageSize = 20)
		{
			return await _context.EAsync<T>(g => g.E().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)]).SkipTake(page * pageSize, pageSize)).ConfigureAwait(false);
		}


		public void Attach(T item)
		{
			_context.Attach<T>(item);
		}

		public async Task DeleteAsync(string id)
		{
			var item = await GetAsync(id).ConfigureAwait(false);
			_context.Delete(item);
		}

		public async Task<IEnumerable<T>> GetAsync(Func<GremlinContext, GremlinQuery> query, int page, int pageSize = 20)
		{
			return await _context.EAsync<T>(context => query.Extend(g => g.SkipTake(page * pageSize, pageSize)).Invoke(context)).ConfigureAwait(false);
		}

		public async Task<IEnumerable<T>> GetAsync(Func<GremlinContext, GremlinQuery> query)
		{
			return await _context.EAsync<T>(query).ConfigureAwait(false);
		}

		public async Task<IEnumerable<T>> GetAsync(int page, int pageSize = 20)
		{
			if (pageSize <= 0) pageSize = 20;
			return await _context.EAsync<T>(g => g.V().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)]).SkipTake(page * pageSize, pageSize)).ConfigureAwait(false);
		}

		public T Create(IVertex inVertex, IVertex outVertex)
		{
			var entity = _context.CreateEntity<T>(Guid.NewGuid().ToString());
			var edge = entity as IEdgeEntityInternal;
			if (edge == null) throw new InvalidOperationException("Unable to construct edge");
			edge.SetInVertex(inVertex);
			edge.SetOutVertex(outVertex);
			edge.Reset(true);
			return entity;

		}

		public async Task<T> GetAsync(string inId, string outId)
		{
			var e = await _context.EAsync<T>(g => g.V(inId.EscapeGremlinString()).BothE().Where(p => p.__().OtherV().HasId(outId.EscapeGremlinString()))).ConfigureAwait(false);
			return e.SingleOrDefault();
		}

		public async Task<IEnumerable<T>> GetByInIdAsync(string inId)
		{

			var e = await _context.EAsync<T>(g =>g.V(inId.EscapeGremlinString()).InE().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)])).ConfigureAwait(false);
			return e;
		}

		public async Task<IEnumerable<T>> GetByOutIdAsync(string outId)
		{
			var e = await _context.EAsync<T>(g => g.V(outId.EscapeGremlinString()).OutE().HasLabel(GraphContextBase._dataSetLabelMapping[typeof(T)])).ConfigureAwait(false);
			return e;
		}
	}
}