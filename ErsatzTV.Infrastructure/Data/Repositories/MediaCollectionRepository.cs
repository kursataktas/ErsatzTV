﻿using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ErsatzTV.Core.Domain;
using ErsatzTV.Core.Interfaces.Repositories;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using static LanguageExt.Prelude;

namespace ErsatzTV.Infrastructure.Data.Repositories
{
    public class MediaCollectionRepository : IMediaCollectionRepository
    {
        private readonly IDbConnection _dbConnection;
        private readonly TvContext _dbContext;

        public MediaCollectionRepository(TvContext dbContext, IDbConnection dbConnection)
        {
            _dbContext = dbContext;
            _dbConnection = dbConnection;
        }

        public async Task<Collection> Add(Collection collection)
        {
            await _dbContext.Collections.AddAsync(collection);
            await _dbContext.SaveChangesAsync();
            return collection;
        }


        public async Task<Unit> AddMediaItem(int collectionId, int mediaItemId)
        {
            Option<Collection> maybeCollection = await _dbContext.Collections
                .Include(c => c.MediaItems)
                .OrderBy(c => c.Id)
                .SingleOrDefaultAsync(c => c.Id == collectionId)
                .Map(Optional);

            await maybeCollection.IfSomeAsync(
                async collection =>
                {
                    if (collection.MediaItems.All(i => i.Id != mediaItemId))
                    {
                        Option<MediaItem> maybeMediaItem = await _dbContext.MediaItems
                            .OrderBy(i => i.Id)
                            .SingleOrDefaultAsync(i => i.Id == mediaItemId)
                            .Map(Optional);

                        await maybeMediaItem.IfSomeAsync(
                            async mediaItem =>
                            {
                                collection.MediaItems.Add(mediaItem);
                                await _dbContext.SaveChangesAsync();
                            });
                    }
                });

            return Unit.Default;
        }

        public Task<Option<Collection>> Get(int id) =>
            _dbContext.Collections
                .OrderBy(c => c.Id)
                .SingleOrDefaultAsync(c => c.Id == id)
                .Map(Optional);

        public Task<Option<Collection>> GetCollectionWithItems(int id) =>
            _dbContext.Collections
                .Include(c => c.MediaItems)
                .ThenInclude(i => i.LibraryPath)
                .Include(c => c.MediaItems)
                .ThenInclude(i => (i as Movie).MovieMetadata)
                .Include(c => c.MediaItems)
                .ThenInclude(i => (i as Show).ShowMetadata)
                .Include(c => c.MediaItems)
                .ThenInclude(i => (i as Season).Show)
                .ThenInclude(s => s.ShowMetadata)
                .Include(c => c.MediaItems)
                .ThenInclude(i => (i as Episode).EpisodeMetadata)
                .Include(c => c.MediaItems)
                .ThenInclude(i => (i as Episode).Season)
                .ThenInclude(s => s.Show)
                .ThenInclude(s => s.ShowMetadata)
                .OrderBy(c => c.Id)
                .SingleOrDefaultAsync(c => c.Id == id)
                .Map(Optional);

        public Task<Option<Collection>> GetCollectionWithItemsUntracked(int id) =>
            _dbContext.Collections
                .AsNoTracking()
                .Include(c => c.MediaItems)
                .ThenInclude(i => i.LibraryPath)
                .Include(c => c.MediaItems)
                .ThenInclude(i => (i as Movie).MovieMetadata)
                .ThenInclude(mm => mm.Artwork)
                .Include(c => c.MediaItems)
                .ThenInclude(i => (i as Show).ShowMetadata)
                .ThenInclude(sm => sm.Artwork)
                .Include(c => c.MediaItems)
                .ThenInclude(i => (i as Season).SeasonMetadata)
                .ThenInclude(sm => sm.Artwork)
                .Include(c => c.MediaItems)
                .ThenInclude(i => (i as Season).Show)
                .ThenInclude(s => s.ShowMetadata)
                .Include(c => c.MediaItems)
                .ThenInclude(i => (i as Episode).EpisodeMetadata)
                .ThenInclude(em => em.Artwork)
                .Include(c => c.MediaItems)
                .ThenInclude(i => (i as Episode).Season)
                .ThenInclude(s => s.Show)
                .ThenInclude(s => s.ShowMetadata)
                .OrderBy(c => c.Id)
                .SingleOrDefaultAsync(c => c.Id == id)
                .Map(Optional);

        public Task<List<Collection>> GetAll() =>
            _dbContext.Collections.ToListAsync();

        public Task<Option<List<MediaItem>>> GetItems(int id) =>
            Get(id).MapT(GetItemsForCollection).Bind(x => x.Sequence());

        public Task Update(Collection collection)
        {
            _dbContext.Collections.Update(collection);
            return _dbContext.SaveChangesAsync();
        }

        public async Task Delete(int collectionId)
        {
            Collection mediaCollection = await _dbContext.Collections.FindAsync(collectionId);
            _dbContext.Collections.Remove(mediaCollection);
            await _dbContext.SaveChangesAsync();
        }

        private async Task<List<MediaItem>> GetItemsForCollection(Collection collection)
        {
            var result = new List<MediaItem>();

            result.AddRange(await GetMovieItems(collection));
            result.AddRange(await GetShowItems(collection));
            result.AddRange(await GetSeasonItems(collection));
            result.AddRange(await GetEpisodeItems(collection));

            return result.Distinct().ToList();
        }

        private async Task<List<Movie>> GetMovieItems(Collection collection)
        {
            IEnumerable<int> ids = await _dbConnection.QueryAsync<int>(
                @"SELECT m.Id FROM CollectionItem ci
            INNER JOIN Movie m ON m.Id = ci.MediaItemId
            WHERE ci.CollectionId = @CollectionId",
                new { CollectionId = collection.Id });

            return await _dbContext.Movies
                .Include(m => m.MovieMetadata)
                .Include(m => m.MediaVersions)
                .Filter(m => ids.Contains(m.Id))
                .ToListAsync();
        }

        private async Task<List<Episode>> GetShowItems(Collection collection)
        {
            IEnumerable<int> ids = await _dbConnection.QueryAsync<int>(
                @"SELECT Episode.Id FROM CollectionItem ci
            INNER JOIN Show ON Show.Id = ci.MediaItemId
            INNER JOIN Season ON Season.ShowId = Show.Id
            INNER JOIN Episode ON Episode.SeasonId = Season.Id
            WHERE ci.CollectionId = @CollectionId",
                new { CollectionId = collection.Id });

            return await _dbContext.Episodes
                .Include(e => e.EpisodeMetadata)
                .Include(e => e.MediaVersions)
                .Include(e => e.Season)
                .Filter(e => ids.Contains(e.Id))
                .ToListAsync();
        }

        private async Task<List<Episode>> GetSeasonItems(Collection collection)
        {
            IEnumerable<int> ids = await _dbConnection.QueryAsync<int>(
                @"SELECT Episode.Id FROM CollectionItem ci
            INNER JOIN Season ON Season.Id = ci.MediaItemId
            INNER JOIN Episode ON Episode.SeasonId = Season.Id
            WHERE ci.CollectionId = @CollectionId",
                new { CollectionId = collection.Id });

            return await _dbContext.Episodes
                .Include(e => e.EpisodeMetadata)
                .Include(e => e.MediaVersions)
                .Include(e => e.Season)
                .Filter(e => ids.Contains(e.Id))
                .ToListAsync();
        }

        private async Task<List<Episode>> GetEpisodeItems(Collection collection)
        {
            IEnumerable<int> ids = await _dbConnection.QueryAsync<int>(
                @"SELECT Episode.Id FROM CollectionItem ci
            INNER JOIN Episode ON Episode.Id = ci.MediaItemId
            WHERE ci.CollectionId = @CollectionId",
                new { CollectionId = collection.Id });

            return await _dbContext.Episodes
                .Include(e => e.EpisodeMetadata)
                .Include(e => e.MediaVersions)
                .Include(e => e.Season)
                .Filter(e => ids.Contains(e.Id))
                .ToListAsync();
        }
    }
}
