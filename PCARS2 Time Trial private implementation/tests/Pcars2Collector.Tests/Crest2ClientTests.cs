using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Pcars2Collector;
using Xunit;

namespace Pcars2Collector.Tests;

public sealed class Crest2ClientTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task ReadsNumericParticipantInvalidationFlag(int rawInvalidation, bool expectedInvalidated)
    {
        var json = $$"""
            {
              "buildinfo": { "mVersion": 9, "mBuildVersionNumber": 1108 },
              "gameStates": { "mGameState": 3, "mSessionState": 1, "mRaceState": 2 },
              "participants": {
                "mViewedParticipantIndex": 0,
                "mNumParticipants": 1,
                "mParticipantInfo": [
                  { "mIsActive": true, "mName": "Rockstar", "mCurrentLap": 3, "mLapsCompleted": 2, "mLapsInvalidated": {{rawInvalidation}} }
                ]
              },
              "vehicleInformation": { "mCarName": "BAC Mono", "mCarClassName": "Road B" },
              "eventInformation": { "mTrackLocation": "Circuit_of_the_Americas", "mTrackVariation": "Club_Circuit" },
              "timings": { "mLapInvalidated": true, "mBestLapTime": -1, "mLastLapTime": 65.061, "mCurrentTime": 6.10101 },
              "timestamp": 1791763929478
            }
            """;
        using var httpClient = new HttpClient(new StaticResponseHandler(json))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        var status = new CollectorStatus();
        var client = new Crest2Client(httpClient, status, NullLogger<Crest2Client>.Instance);

        var snapshot = await client.ReadAsync(CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(expectedInvalidated, snapshot!.PlayerLapInvalidated);
        Assert.Equal((uint)2, snapshot.LapsCompleted);
        Assert.Equal(65.061, snapshot.LastLapTimeSeconds, 3);
    }

    private sealed class StaticResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }
}
