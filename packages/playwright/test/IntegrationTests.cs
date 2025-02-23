#nullable enable

using Deque.AxeCore.Commons;
using Microsoft.Playwright.NUnit;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Deque.AxeCore.Playwright.Test
{
    [TestFixture]
    [Category("Integration")]
    public class IntegrationTests : PageTest
    {
        private readonly TestServer m_testServer;

        public IntegrationTests()
        {
            m_testServer = new();
        }

        [OneTimeSetUp]
        public async Task InitializeTest()
        {
            await m_testServer.StartAsync();
        }

        [OneTimeTearDown]
        public async Task CleanupTest()
        {
            await m_testServer.StopAsync();
        }

        static object?[] GetAxeRulesCases = {
            null,
            new List<string>() { "wcag2aa", "wcag2a" },
        };

        [Test]
        [TestCaseSource(nameof(GetAxeRulesCases))]
        public async Task GetAxeRules_WithTags_ReturnsAxeRules(IList<string>? tags)
        {
            await NavigateToPage("basic.html");
            IList<AxeRuleMetadata> axeRules = await Page!.GetAxeRules(tags);

            AxeRuleMetadata? rule = axeRules.FirstOrDefault();

            Assert.Multiple(() =>
            {
                Assert.That(rule, Is.Not.Null);
                Assert.That(rule!.RuleId, Is.Not.Null);
                Assert.That(rule.Description, Is.Not.Null);
                Assert.That(rule.Help, Is.Not.Null);
                Assert.That(rule.HelpUrl, Is.Not.Null);
                if (tags != null)
                {
                    Assert.That(axeRules.All(axeRule => tags.Any(tag => axeRule.Tags.Contains(tag))));
                }
            });
        }

        [Test]
        public async Task RunAxe_NoOptions()
        {
            const string expectedViolationId = "color-contrast";

            await NavigateToPage("basic.html");

            AxeResult axeResults = await Page!.RunAxe();
            AxeResultItem violation = axeResults.Violations.First();

            Assert.Multiple(() =>
            {
                Assert.That(violation.Id, Is.EqualTo(expectedViolationId));
                Assert.That(violation.Impact, Is.EqualTo("serious"));
                Assert.That(violation.Description, Is.Not.Null.Or.Empty);
                Assert.That(violation.Help, Is.Not.Null.Or.Empty);
            });
        }

        [Test]
        public async Task RunAxe_WithRunOnly_RunsOnlySpecifiedTags()
        {
            await NavigateToPage("basic.html");

            AxeRunOptions expectedOptions = new AxeRunOptions()
            {
                RunOnly = new RunOnlyOptions()
                {
                    Type = "tag",
                    Values = new List<string>() { "wcag2a" }
                }
            };

            AxeResult axeResults = await Page!.RunAxe(expectedOptions);

            IEnumerable<AxeResultItem> violationsWithoutExpectedTag = axeResults.Violations.Where(violation => !violation.Tags!.Any(tag => tag == "wcag2a"));
            Assert.That(violationsWithoutExpectedTag, Is.Empty);
        }

        [Test]
        public async Task RunAxe_WithRunOnly_RunsOnlySpecifiedRules()
        {
            await NavigateToPage("basic.html");

            AxeRunOptions expectedOptions = new AxeRunOptions()
            {
                RunOnly = new RunOnlyOptions()
                {
                    Type = "rule",
                    Values = new List<string>() { "color-contrast" }
                }
            };

            AxeResult axeResults = await Page!.RunAxe(expectedOptions);

            IEnumerable<AxeResultItem> inapplicablesForUnexpectedRule = axeResults.Inapplicable.Where(result => result.Id != "color-contrast");
            Assert.That(inapplicablesForUnexpectedRule, Is.Empty);
        }

        [Test]
        public async Task RunAxe_WithRules_RunsOnlySpecified()
        {
            await NavigateToPage("basic.html");
            const string ruleId = "color-contrast";

            Dictionary<string, RuleOptions> rules = new Dictionary<string, RuleOptions>()
            {
                { ruleId, new RuleOptions() { Enabled = false } }
            };

            AxeRunOptions expectedOptions = new AxeRunOptions()
            {
                Rules = rules
            };

            AxeResult axeResults = await Page!.RunAxe(expectedOptions);

            Assert.Multiple(() =>
            {
                Assert.That(axeResults.Passes.Any(result => result.Id.Equals(ruleId)), Is.False);
                Assert.That(axeResults.Violations.Any(result => result.Id.Equals(ruleId)), Is.False);
                Assert.That(axeResults.Incomplete.Any(result => result.Id.Equals(ruleId)), Is.False);
                Assert.That(axeResults.Inapplicable.Any(result => result.Id.Equals(ruleId)), Is.False);
            });
        }

        [Test]
        public async Task RunAxe_WithResultTypes_RunsOnlySpecified()
        {
            await NavigateToPage("basic.html");

            HashSet<ResultType> resultGroups = new HashSet<ResultType>()
            {
               ResultType.Passes
            };

            AxeRunOptions expectedOptions = new AxeRunOptions()
            {
                ResultTypes = resultGroups
            };

            AxeResult axeResults = await Page!.RunAxe(expectedOptions);

            Assert.Multiple(() =>
            {
                Assert.That(axeResults.Violations.First(v => v.Id.Equals("color-contrast")).Nodes, Has.Length.EqualTo(1),
                            "There are two color-contrast issues, but the Nodes should be capped at 1.");
                Assert.That(axeResults.Passes.First(v => v.Id.Equals("region")).Nodes, Has.Length.GreaterThan(1));
            });
        }

        [Test]
        public async Task RunAxe_WithRunOptions_SelectorsFalse_ProducesResultsWithoutTargets()
        {
            await NavigateToPage("basic.html");

            AxeResult axeResults = await Page!.RunAxe(new AxeRunOptions() { Selectors = false });

            var violationsWithTargets = axeResults.Violations.Where(violation => violation.Nodes!.Any(node => node.Target != null && node.Target.Any()));
            Assert.That(violationsWithTargets, Is.Empty);
        }

        [Test]
        public async Task RunAxe_WithRunOptions_AncestryTrue_ProducesResultsWithAncestry()
        {
            await NavigateToPage("basic.html");

            AxeRunOptions expectedOptions = new AxeRunOptions()
            {
                Ancestry = true
            };

            AxeResult axeResults = await Page!.RunAxe(expectedOptions);

            var violationsWithoutAncestry = axeResults.Violations.Where(violation => violation.Nodes!.Any(node => node.Ancestry == null || !node.Ancestry.Any()));
            Assert.That(violationsWithoutAncestry, Is.Empty);
        }

        [Test]
        public async Task RunAxe_WithRunOptions_XPathTrue_ProducesResultsWithXPath()
        {
            await NavigateToPage("basic.html");

            AxeRunOptions expectedOptions = new AxeRunOptions()
            {
                XPath = true
            };

            AxeResult axeResults = await Page!.RunAxe(expectedOptions);
            var violationsWithoutXPath = axeResults.Violations.Where(violation => violation.Nodes!.Any(node => node.XPath == null || !node.XPath.Any()));
            Assert.That(violationsWithoutXPath, Is.Empty);
        }

        [Test]
        public async Task RunAxe_NestedIframes_FindsNestedViolationsByDefault()
        {
            const string expectedViolationId = "aria-roles";
            const string expectedViolationTarget = "#div-fail";

            await NavigateToPage("with-frame.html");

            AxeResult axeResults = await Page!.RunAxe();

            Assert.That(axeResults.Violations, Has.Length.EqualTo(1));
            AxeResultItem ariaViolation = axeResults.Violations.First();
            List<AxeResultTarget> targets = ariaViolation.Nodes!.First().Target;

            Assert.Multiple(() =>
            {
                Assert.That(ariaViolation.Id, Is.EqualTo(expectedViolationId));
                Assert.That(targets, Is.Not.Null.Or.Empty);
                Assert.That(targets.Any(target => target.ToString().Contains(expectedViolationTarget)));
            });
        }

        [Test]
        [TestCase("button")]
        [TestCase("text=Text example")]
        [TestCase("[aria-label='Accessibility Label']")]
        [TestCase("id=id-example")]
        public async Task RunAxeOnLocator_NoOptions(string selector)
        {
            await NavigateToPage("selector.html");

            AxeResult axeResults = await Page!.Locator(selector).RunAxe();
            Assert.That(axeResults.Passes.All(pass => pass.Nodes!.Length == 1));
        }

        [Test]
        public async Task RunAxeOnLocator_WithOptions()
        {
            const string tag = "ACT";
            await NavigateToPage("selector.html");

            AxeRunOptions expectedOptions = new AxeRunOptions()
            {
                RunOnly = new RunOnlyOptions()
                {
                    Type = "tag",
                    Values = new List<string>() { tag }
                }
            };

            AxeResult axeResults = await Page!.Locator("button").RunAxe(expectedOptions);
            Assert.That(axeResults.Passes.All(pass => pass.Tags!.Contains(tag)));
        }

        [Test]
        [TestCaseSource(nameof(RunAxe_WithContext_Cases))]
        public async Task RunAxe_WithContext(AxeRunContext axeRunContext, ISet<string> targets, bool includedInTargets)
        {
            await NavigateToPage("selector.html");

            AxeResult axeResults = await Page!.RunAxe(axeRunContext);

            Assert.That(axeResults.Passes
                .All(pass => pass.Nodes!
                .All(node => node.Target!
                .Any(target => includedInTargets == targets.Contains(target.ToString())))));
        }

        static object[] RunAxe_WithContext_Cases = {
            new object[]
            {
                new AxeRunContext()
                {
                    Include = new List<string[]> { new string[] { "#id-example" } },
                    Exclude = null
                },
                new HashSet<string>()
                {
                    "#id-example"
                },
                true
            },
            new object[]
            {
                new AxeRunContext()
                {
                    Include = new List<string[]> { new string[] { "a" } },
                    Exclude = null
                },
                new HashSet<string>()
                {
                    "a[aria-label=\"Accessibility Label\"]",
                    "#id-example"
                },
                true
            },
            new object[]
            {
                new AxeRunContext()
                {
                    Include = null,
                    Exclude = new List<string[]> { new string[] { "#id-example" } }
                },
                new HashSet<string>()
                {
                    "#id-example"
                },
                false
            },
            new object[]
            {
                new AxeRunContext()
                {
                    Include = null,
                    Exclude = new List<string[]> { new string[] { "a" } }
                },
                new HashSet<string>()
                {
                    "a[aria-label=\"Accessibility Label\"]",
                    "#id-example"
                },
                false
            },
        };

        private async Task NavigateToPage(string htmlPageName)
        {
            Uri uri = new(TestServer.BaseUri, htmlPageName);
            await Page!.GotoAsync(uri.ToString());
        }
    }
}
