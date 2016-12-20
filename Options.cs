using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace Toolfactory.Vsts.BuidDefinitionProcessor
{
    class Options
    {
        [Option('o', "my-option", DefaultValue = 10, HelpText = "This is an option!")]
        public int MyOption { get; set; }
    }
}
