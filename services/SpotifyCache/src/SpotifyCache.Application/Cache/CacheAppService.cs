﻿using Abp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using SpotifyCache.Cache.Dto;
using SpotifyCache.Domain;
using SpotifyCache.Domain.Tracks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotifyCache.Cache
{
    public class CacheAppService : SpotifyCacheAppServiceBase, ICacheAppService
    {
        private readonly IRepository<Artist, string> _artistRepository;
        private readonly IRepository<Track, string> _trackRepository;

        public CacheAppService(IRepository<Artist, string> artistRepository, IRepository<Track, string> trackRepository)
        {
            _artistRepository = artistRepository;
            _trackRepository = trackRepository;
        }

        public async Task AddTracks(AddTracksInputDto input)
        {
            foreach(var track in input.Tracks)
            {
                await _trackRepository.InsertAsync(ObjectMapper.Map<Track>(track));
            }
            var artistsToUpdate = _artistRepository.GetAll()
                    .Where(x => input.ArtistIds.Contains(x.Id));
            foreach(var artist in artistsToUpdate)
            {
                artist.LastSearchedSongs = DateTime.UtcNow;
            }
        }

        public List<string> TracksToAdd(TracksToAddDto input)
        {
            var query = _trackRepository.GetAll()
                .Select(x => x.Id)
                .Where(x => input.Tracks.Contains(x))
                .ToList();
            var excluded = input.Tracks.Except(query).ToList();

            return excluded;
        }

        public async Task<List<RelatedArtistOutputDto>> RelatedArtists(RelatedArtistInputDto input)
        {
            var ids = input.Artists.Select(x => x.Id).ToList();
            var existingArtists = await _artistRepository.GetAll()
                .Where(x => ids.Contains(x.Id))
                .ToListAsync();
            var timeWindow = DateTime.UtcNow.Subtract(TimeSpan.FromDays(90));
            var results = new List<RelatedArtistOutputDto>();
            foreach(var dto in input.Artists)
            {
                var entity = existingArtists.FirstOrDefault(x => x.Id == dto.Id);
                if(entity == null)
                {
                    entity = ObjectMapper.Map<Artist>(dto);
                    await _artistRepository.InsertAsync(entity);
                }else if(!entity.SearchedRelatedArtists && dto.SearchedRelatedArtists)
                {
                    entity.SearchedRelatedArtists = true;
                }
                if(entity.LastSearchedSongs == null || entity.LastSearchedSongs < timeWindow)
                {
                    var output = new RelatedArtistOutputDto
                    {
                        Id = entity.Id,
                        ShouldSearchRelated = false,
                    };
                    if(input.MoreCount > 0 && !entity.SearchedRelatedArtists)
                    {
                        output.ShouldSearchRelated = true;
                        input.MoreCount--;
                    }
                    results.Add(output);
                }
            }
            return results;
        }
    }
}
