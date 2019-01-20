using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace ProjectCeilidh.SilverTongue.Tests
{
    public class SilverTongueTests
    {
        private readonly SilverTongue _localization;

        public SilverTongueTests()
        {
            _localization = new SilverTongue(CultureInfo.InvariantCulture);
            _localization.Extend(new Dictionary<string, string[]>
            {
                ["test.plural"] = new[] {"{0} item", "{0} items"},
                ["test.interpolate"] = new[] {"{0}, your name is {0}!"},
                ["test.multi"] = new[] {"{0} {1}"}
            });
        }

        [Fact]
        public void Pluralization()
        {
            Assert.Equal("0 items", _localization.Translate("test.plural", 0));
            Assert.Equal("1 item", _localization.Translate("test.plural", 1));
            Assert.Equal("2 items", _localization.Translate("test.plural", 2));
        }

        [Fact]
        public void Interpolation()
        {
            Assert.Equal("SilverTongue, your name is SilverTongue!", _localization.Translate("test.interpolate", "SilverTongue"));
        }

        [Fact]
        public void MissingKey()
        {
            Assert.Equal("test.missing", _localization.Translate("test.missing"));
        }

        [Fact]
        public void MissingValue()
        {
            Assert.Equal("{0} {1}", _localization.Translate("test.multi"));
        }
    }
}