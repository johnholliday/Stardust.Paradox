﻿using Newtonsoft.Json.Linq;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.CodeGeneration;
using Stardust.Paradox.Data.Traversals;
using Stardust.Particles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data.Internals
{
    internal class EdgeCollection<TTout> : IEdgeCollection<TTout> where TTout : IVertex
    {
        private readonly string _edgeLabel;
        private readonly string _gremlinQuery;
        private readonly IGraphContext _context;
        private readonly GraphDataEntity _parent;
        private readonly string _reverseLabel;

        internal bool IsDirty
        {
            get => _isDirty;
            set
            {
                if (value) _parent.IsDirty = true;
                _isDirty = value;
            }
        }

        public async Task SaveChangesAsync()
        {
            if (!IsDirty) return;
            foreach (var edge in _addedCollection)
            {
                await edge.AddToVertexAsync(_edgeLabel ?? _reverseLabel).ConfigureAwait(false);
            }
            _addedCollection.Clear();
            foreach (var edge in _deletedCollection)
            {
                await edge.DropEdgeAsync(_edgeLabel ?? _reverseLabel).ConfigureAwait(false);
            }
            _deletedCollection.Clear();
            IsDirty = false;
        }

        internal EdgeCollection(string edgeLabel, IGraphContext context, GraphDataEntity parent, string reverseLabel)
        {
            _edgeLabel = edgeLabel;
            _context = context;
            _parent = parent;
            _reverseLabel = reverseLabel;
        }

        internal EdgeCollection(string gremlinQuery, IGraphContext context, GraphDataEntity parent)
        {
            _gremlinQuery = gremlinQuery;
            _context = context;
            _parent = parent;
        }

        private readonly ICollection<IEdge<TTout>> _collection = new List<IEdge<TTout>>();
        private readonly ICollection<IEdge<TTout>> _edgeCollection = new List<IEdge<TTout>>();
        private readonly ICollection<Edge<TTout>> _addedCollection = new List<Edge<TTout>>();
        private readonly ICollection<Edge<TTout>> _deletedCollection = new List<Edge<TTout>>();
        protected bool _isLoaded;
        private bool _isDirty;
        private string _referenceType;

        public async Task<IEnumerable<TTout>> ToVerticesAsync()
        {
            await LoadAsync().ConfigureAwait(false);
            return _collection?.Select(e => e.Vertex) ?? new List<TTout>();
        }

        public async Task<IEnumerable<TTout>> ToVerticesAsync(Expression<Func<TTout, object>> filterSelector, object value)
        {
            await LoadAsync(filterSelector, value).ConfigureAwait(false);
            return _collection?.Select(e => e.Vertex) ?? new List<TTout>();
        }

        public async Task<IEnumerable<IEdge<TTout>>> ToEdgesAsync()
        {
            await LoadEdges().ConfigureAwait(false);
            return _edgeCollection;
        }



        IEnumerator<TTout> IEnumerable<TTout>.GetEnumerator()
        {
            foreach (var edge in _collection)
            {
                yield return edge.Vertex;
            }
        }

        public IEnumerator<IEdge<TTout>> GetEnumerator()
        {
            if (!_isLoaded /*&& _parent._eagerLoding*/)
            {
                Load();
            }
            return _collection.GetEnumerator();
        }

        private void Load()
        {
            if (GraphConfiguration.UseSafeAsync)
            {
                Task.Run(async () => await LoadAsync().ConfigureAwait(false))
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            }
            LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task LoadAsync()
        {
            try
            {
                if (_isLoaded) return;
                IsDirty = false;
                _addedCollection.Clear();
                _deletedCollection.Clear();
                var v = await GetEdgeContent().ConfigureAwait(false);
                _collection.Clear();
                if (v != null)
                    foreach (var tout in from i in v where i != null select i)
                    {
                        _collection.Add(new Edge<TTout>((tout as GraphDataEntity)._entityKey, tout, _parent, _context) { EdgeType = _referenceType });
                    }
                _isLoaded = true;
            }
            catch (Exception ex)
            {
                ex.Log();
                throw;
            }

        }

        public async Task LoadAsync(Expression<Func<TTout, object>> selector, object value)
        {

            // Needs Refactoring to avoid duplicated Code.
            try
            {
                var prop = (PropertyInfo)((MemberExpression)selector.Body).Member;
                if (_isLoaded) return;
                IsDirty = false;
                _addedCollection.Clear();
                _deletedCollection.Clear();
                var v = await GetEdgeContent(prop, value).ConfigureAwait(false);
                _collection.Clear();
                if (v != null)
                    foreach (var tout in from i in v where i != null select i)
                    {
                        _collection.Add(new Edge<TTout>((tout as GraphDataEntity)._entityKey, tout, _parent, _context) { EdgeType = _referenceType });
                    }
                _isLoaded = true;
            }
            catch (Exception ex)
            {
                ex.Log();
                throw;
            }
        }

        private async Task LoadEdges()
        {
            try
            {
                IsDirty = false;
                _addedCollection.Clear();
                _deletedCollection.Clear();
                var v = await GetEdgeonly().ConfigureAwait(false);
                _edgeCollection.Clear();
                if (v != null)
                    foreach (var edge in from i in v where i != null select i)
                    {
                        JObject tout = (JObject)edge;
                        var e = new Edge<TTout>(tout["id"].Value<string>(), _parent, _context) { EdgeType = _referenceType };
                        //e.Properties.AddRange(tout["properties"].Values());
                        foreach (dynamic va in tout["properties"])
                        {
                            e.Properties.Add(va.Name, va.Value);
                            // va.
                        }
                        _edgeCollection.Add(e);

                    }
                _isLoaded = true;
            }
            catch (Exception ex)
            {
                ex.Log();
                throw;
            }
        }


        private async Task<IEnumerable<TTout>> GetEdgeContent()
        {
            try
            {
                if (_gremlinQuery.ContainsCharacters()) return await _context.VAsync<TTout>(g => new GremlinQuery(g._connector, _gremlinQuery.Replace("{id}", _parent._entityKey))).ConfigureAwait(false);
                if (_reverseLabel != null && _reverseLabel != _edgeLabel)
                    return await _context.VAsync<TTout>(ReverseLabelQuery()).ConfigureAwait(false);
                if (_reverseLabel != null && _reverseLabel == _edgeLabel)
                    return await _context.VAsync<TTout>(EdgeLabelQuery()).ConfigureAwait(false);
                return await _context.VAsync<TTout>(SimpleEdgeLabelQuery()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ex.Log("");
                throw;
            }
        }

        private async Task<IEnumerable<TTout>> GetEdgeContent(PropertyInfo filterProperty = null, object value = null) //Expression < Func<TTout, object>> selector, object value)
        {
            try
            {
               
                if (_gremlinQuery.ContainsCharacters())
                {
                    IEnumerable<TTout> enumerable = await _context.VAsync<TTout>(g =>
                    {
                        return new GremlinQuery(g._connector, $"{_gremlinQuery.Replace("{id}", _parent._entityKey)}.has('{filterProperty.Name.ToCamelCase()}', '{Update.GetValue(value).Trim('\'')}')");
                    }).ConfigureAwait(false);
                    return enumerable;

                }

                if (_reverseLabel != null && _reverseLabel != _edgeLabel)
                {
                    return await _context.VAsync<TTout>(ReverseLabelQuery(filterProperty, value)).ConfigureAwait(false);
                }

                if (_reverseLabel != null && _reverseLabel == _edgeLabel)
                {
                    return await _context.VAsync<TTout>(EdgeLabelQuery(filterProperty, value)).ConfigureAwait(false);
                }

                return await _context.VAsync<TTout>(SimpleEdgeLabelQuery(filterProperty, value)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ex.Log("");
                throw;
            }
        }

        private async Task<IEnumerable<dynamic>> GetEdgeonly()
        {
            try
            {
                if (_gremlinQuery.ContainsCharacters()) return await _context.ExecuteAsync<dynamic>(g => new GremlinQuery(g._connector, _gremlinQuery.Replace("{id}", _parent._entityKey))).ConfigureAwait(false);
                if (_reverseLabel != null && _reverseLabel != _edgeLabel)
                    return await _context.ExecuteAsync<dynamic>(ReverseLabelQueryEdgeOnly()).ConfigureAwait(false);
                if (_reverseLabel != null && _reverseLabel == _edgeLabel)
                    return await _context.ExecuteAsync<dynamic>(EdgeLabelQueryEdgeOnly()).ConfigureAwait(false);
                return await _context.ExecuteAsync<dynamic>(SimpleEdgeLabelQueryEdgeOnly()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ex.Log("");
                throw;
            }
        }

        private async Task<IEnumerable<TTout>> GetEdge()
        {
            try
            {
                if (_gremlinQuery.ContainsCharacters()) return await _context.VAsync<TTout>(g => new GremlinQuery(g._connector, _gremlinQuery.Replace("{id}", _parent._entityKey))).ConfigureAwait(false);
                if (_reverseLabel != null && _reverseLabel != _edgeLabel)
                    return await _context.VAsync<TTout>(ReverseLabelQuery()).ConfigureAwait(false);
                if (_reverseLabel != null && _reverseLabel == _edgeLabel)
                    return await _context.VAsync<TTout>(EdgeLabelQuery()).ConfigureAwait(false);
                return await _context.VAsync<TTout>(SimpleEdgeLabelQuery()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ex.Log("");
                throw;
            }
        }

        private Func<GremlinContext, GremlinQuery> SimpleEdgeLabelQuery(PropertyInfo filterProperty = null, object value = null)
        {
            _referenceType = "out";

            return g =>
            {
                var gQuery = GetSeletor(g).In(_edgeLabel);

                if (filterProperty != null)
                {

                    gQuery = gQuery.Has($"{filterProperty.Name.ToCamelCase()}", Update.GetValue(value));
                }
                return gQuery;


            };//.OutV();
        }

        private Func<GremlinContext, GremlinQuery> EdgeLabelQuery(PropertyInfo filterProperty = null, object value = null)
        {
            _referenceType = "both";
            return g =>
            {
                var gQuery = GetSeletor(g).As("i").BothE(_edgeLabel).BothV();

                if (filterProperty != null)
                {

                    gQuery = gQuery.Has($"{filterProperty.Name.ToCamelCase()}", Update.GetValue(value).Trim('\''));
                }
                gQuery = gQuery.Where(p => p.P.Not(q => q.Eq("i")));
                return gQuery;


                };
        }

        private Func<GremlinContext, GremlinQuery> ReverseLabelQuery(PropertyInfo filterProperty = null, object value = null)
        {
            _referenceType = "in";
            return g =>
            {
                var gQuery = GetSeletor(g).Out(_reverseLabel);

                if (filterProperty != null)
                {

                    gQuery = gQuery.Has($"{filterProperty.Name.ToCamelCase()}", Update.GetValue(value).Trim('\''));
                }
                return gQuery;

            };//.InV();
        }

        private Func<GremlinContext, GremlinQuery> SimpleEdgeLabelQueryEdgeOnly()
        {
            _referenceType = "out";
            return g => GetSeletor(g).InE(_edgeLabel);
        }

        private Func<GremlinContext, GremlinQuery> EdgeLabelQueryEdgeOnly()
        {
            _referenceType = "both";

            return g => GetSeletor(g).As("i").BothE(_edgeLabel).As("e").BothV().Where(p => p.P.Not(q => q.Eq("i")));
        }

        private GremlinQuery GetSeletor(GremlinContext g)
        {
            return _parent._partitionKey.ContainsCharacters() ? g.V(_parent.EntityKey, _parent._partitionKey) : g.V(_parent._entityKey);
        }

        private Func<GremlinContext, GremlinQuery> ReverseLabelQueryEdgeOnly()
        {
            _referenceType = "in";
            return g => g.V(_parent._entityKey).OutE(_reverseLabel);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(IEdge<TTout> item)
        {
            var edge = item as Edge<TTout>;
            if (_gremlinQuery.ContainsCharacters()) throw new InvalidOperationException("Unable to insert edge on query navigation property");
            _addedCollection.Add(item as Edge<TTout>);
            _collection.Add(item);
            IsDirty = true;
        }

        public void Add(TTout vertex)
        {
            Add(new Edge<TTout>("", vertex, _parent, _context) { AddReverse = _reverseLabel.ContainsCharacters() });
        }

        public void Add(TTout vertex, IDictionary<string, object> edgeProperties)
        {
            var edge = new Edge<TTout>("", vertex, _parent, _context) { AddReverse = _reverseLabel.ContainsCharacters() };
            edge.Properties.AddRange(edgeProperties);
            Add(edge);
        }

        public void AddDual(TTout vertex)
        {
            Add(new Edge<TTout>("", vertex, _parent, _context) { AddReverse = true, ReverseLabel = _reverseLabel });
        }

        public void Clear()
        {
            _collection.Clear();
        }

        public bool Contains(TTout item)
        {
            var e = item as GraphDataEntity;
            return _collection.Any(Predicate(e));
        }

        public void CopyTo(TTout[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(TTout item)
        {
            var e = item as GraphDataEntity;
            foreach (var edge in _collection.Where(Predicate(e)).Select(i => i).ToArray())
            {
                _collection.Remove(edge);
                _deletedCollection.Add(edge as Edge<TTout>);
                IsDirty = true;
            }
            return true;
        }

        private static Func<IEdge<TTout>, bool> Predicate(GraphDataEntity e)
        {
            return i =>
            {
                var r = i.Vertex as GraphDataEntity;
                return r._entityKey == e._entityKey;
            };
        }

        public bool Contains(IEdge<TTout> item) => _collection.Contains(item);

        public void CopyTo(IEdge<TTout>[] array, int arrayIndex) => _collection.CopyTo(array, arrayIndex);

        public bool Remove(IEdge<TTout> item)
        {
            _deletedCollection.Add(item as Edge<TTout>);

            var result = _collection.Remove(item);
            if (result)
                IsDirty = true;
            return result;
        }

        public int Count
        {
            get
            {
                if (!_isLoaded) Load();
                var collectionCount = _collection.Count;
                return collectionCount;
            }
        }

        public bool IsReadOnly => _gremlinQuery.ContainsCharacters();
    }
}