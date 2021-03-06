// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using FluentAssertions;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace Its.Log.Monitoring.UnitTests
{
    [TestFixture]
    public class TargetBasedTestConstraintTests
    {
        [SetUp]
        public void SetUp()
        {
            TestsConstrainedToTarget.AppliesTo = _ => false;
        }

        private static TestApi CreateApiClient()
        {
            return new TestApi(targets =>
                                   targets.Add("staging", "widgetapi",
                                               new Uri("http://staging.widgets.com"),
                                               dependencies =>
                                                   dependencies.Register(() =>
                                                                             new HttpClient(new HttpServer(new HttpConfiguration()
                                                                                                               .MapSensorRoutes(_ => true))))),
                               typeof (TestsConstrainedToTarget));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (DictionaryEntry entry in HttpRuntime.Cache)
            {
                HttpRuntime.Cache.Remove((string) entry.Key);
            }
        }

        [Test]
        public async Task A_test_can_be_hidden_based_on_the_target_application_build_date_sensor()
        {
            TestsConstrainedToTarget.AppliesTo = BuildDateAfter(DateTime.Now);

            var response = await CreateApiClient().GetAsync("http://tests.com/tests");

            JArray tests = response.JsonContent().Tests;

            tests.Should().NotContain(t => t.Value<string>("Url").Contains("target_based_constraint_test"));
        }

        [Test]
        public async Task A_test_can_be_shown_based_on_the_target_application_build_date_sensor()
        {
            TestsConstrainedToTarget.AppliesTo = BuildDateAfter(DateTime.Now.Subtract(TimeSpan.FromDays(1000)));

            var response = await CreateApiClient().GetAsync("http://tests.com/tests");

            JArray tests = response.JsonContent().Tests;

            tests.Should().Contain(t => t.Value<string>("Url").Contains("target_based_constraint_test"));
        }

        [Test]
        public async Task Target_constraints_cache_their_results_and_and_do_not_trigger_every_time_Match_is_called()
        {
            var constraintCalls = 0;

            TestsConstrainedToTarget.AppliesTo = _ =>
            {
                Interlocked.Increment(ref constraintCalls);
                return false;
            };

            var apiClient = CreateApiClient();

            await apiClient.GetAsync("http://tests.com/tests");
            await apiClient.GetAsync("http://tests.com/tests");
            await apiClient.GetAsync("http://tests.com/tests");

            constraintCalls.Should().BeLessOrEqualTo(1);
        }

        private static Func<HttpClient, bool> BuildDateAfter(DateTime dateTime)
        {
            return httpClient =>
            {
                var sensorResult = httpClient.GetAsync("/sensors").Result.JsonContent();
                Console.WriteLine(sensorResult);
                var buildDate = sensorResult.Version["Build date"];
                return buildDate > dateTime;
            };
        }
    }

    public class TestsConstrainedToTarget : IApplyToTarget
    {
        private readonly HttpClient httpClient;

        public TestsConstrainedToTarget(HttpClient httpClient)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }
            this.httpClient = httpClient;
        }

        public void target_based_constraint_test()
        {
        }

        public bool AppliesToTarget(TestTarget target)
        {
            return AppliesTo(httpClient);
        }

        public static Func<HttpClient, bool> AppliesTo = _ => false;
    }
}