using System.Collections.Concurrent;
using System.Diagnostics;
using NetStone;
using NetStone.Model.Parseables.Character.ClassJob;
using NetStone.Model.Parseables.FreeCompany;
using NetStone.Model.Parseables.FreeCompany.Members;
using NetStone.Model.Parseables.Search.FreeCompany;
using NetStone.Search.FreeCompany;
using NetStone.StaticData;

FileVersionInfo version = FileVersionInfo.GetVersionInfo(Environment.GetCommandLineArgs()[0]);
Console.WriteLine($"FreeCompanyRegister - v{version.ProductVersion}");

const string languageArg = "/language:";

string? query = null;
Language language = Language.eu;

foreach (string arg in Environment.GetCommandLineArgs().Skip(1))
{
    if (arg.StartsWith(languageArg))
    {
        if (!Enum.TryParse(arg.Substring(languageArg.Length), out language))
        {
            Console.WriteLine($"Unknow language \"{arg}\". Choose one of those: {string.Join(", ", Enum.GetNames<Language>())}");
        }
    }
    else if (query is null)
    {
        query = arg;
    }
}

Console.WriteLine($"Lodestone language: {language}");

using LodestoneClient client = await LodestoneClient.GetClientAsync(lodestoneBaseAddress: $"https://{language}.finalfantasyxiv.com");

// Search free company by name or Lodestone ID
string? freeCompanyQuery = query;
bool hasCommandLineQuery = freeCompanyQuery is not null;

string freeCompanyId;
string freeCompanyName;
string freeCompanyServer;
while (true)
{
    Console.WriteLine();

    if (string.IsNullOrEmpty(query))
    {
        do
        {
            Console.Write("Enter the Free Company name (or Lodestone ID): ");
            freeCompanyQuery = Console.ReadLine();
        } while (string.IsNullOrWhiteSpace(freeCompanyQuery));
    }

    freeCompanyQuery = freeCompanyQuery?.Trim() ?? string.Empty;
    if (freeCompanyQuery.All(char.IsDigit))
    {
        LodestoneFreeCompany? freeCompany = await client.GetFreeCompany(freeCompanyQuery);
        if (freeCompany is null)
        {
            Console.WriteLine($"There is no free company matching the ID {freeCompanyQuery}.");
            if (hasCommandLineQuery)
                return;
            continue;
        }
        
        freeCompanyId = freeCompany.Id;
        freeCompanyName = freeCompany.Name;
        freeCompanyServer = freeCompany.World;
    }
    else
    {
        Console.WriteLine($"""Search "{freeCompanyQuery}"...""");

        FreeCompanySearchPage? freeCompanySearch = await client.SearchFreeCompany(new FreeCompanySearchQuery()
        {
            Name = freeCompanyQuery,
            SortKind = SortKind.MembershipHighToLow
        });

        if (freeCompanySearch is null)
        {
            Console.WriteLine($"""Failed to search a free company with query "{freeCompanyQuery}".""");
            if (hasCommandLineQuery)
                return;
            continue;
        }

        if (!freeCompanySearch.HasResults)
        {
            Console.WriteLine($"""No match for free company with query "{freeCompanyQuery}".""");
            if (hasCommandLineQuery)
                return;
            continue;
        }

        int searchResultIndex;
        if (freeCompanySearch.Results.Count() == 1)
        {
            searchResultIndex = 1;
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine($"""Search result for "{freeCompanyQuery}":""");
        
            int resultCount = 0;
            foreach (FreeCompanySearchEntry result in freeCompanySearch.Results)
            {
                Console.WriteLine($"{resultCount + 1}- {result.Name} ({result.Server})");
                resultCount++;
            }
            Console.WriteLine();
    
            string? selectedSearchResultId;
            do
            {
                Console.Write("Enter the number (0 to quit): ");
                selectedSearchResultId = Console.ReadLine();

                if (selectedSearchResultId == 0.ToString())
                    return;

            } while (string.IsNullOrWhiteSpace(selectedSearchResultId)
                     || !int.TryParse(selectedSearchResultId.Trim(), out searchResultIndex)
                     || searchResultIndex < 0
                     || searchResultIndex > resultCount);
        }
    
        var selectedFreeCompany = freeCompanySearch.Results.ElementAt(searchResultIndex - 1);
        freeCompanyId = selectedFreeCompany.Id!;
        freeCompanyName = selectedFreeCompany.Name;
        freeCompanyServer = selectedFreeCompany.Server;
    }

    break;
}

DateTime dateTime = DateTime.Now;

FreeCompanyMembers? membersRequest = null;
List<FreeCompanyMembersEntry> membersInfos = new();
do
{
    var page = (membersRequest?.CurrentPage ?? 0) + 1;
    Console.WriteLine($"""Get "{freeCompanyName}" ({freeCompanyServer}) members - Page {page}...""");
    
    membersRequest = await client.GetFreeCompanyMembers(freeCompanyId, page);
    if (membersRequest is null)
    {
        Console.WriteLine($"""Failed to get {freeCompanyName}" ({freeCompanyServer}) members (page {page}).""");
        return;
    }

    foreach (FreeCompanyMembersEntry memberInfo in membersRequest.Members)
    {
        membersInfos.Add(memberInfo);
    }
}
while (membersRequest.CurrentPage < membersRequest.NumPages);

ConcurrentBag<Member> members = new();

Console.Write("Get members infos...");
await Parallel.ForEachAsync(membersInfos, async (memberInfo, _) =>
{
    CharacterClassJob? jobInfos = await client.GetCharacterClassJob(memberInfo.Id);
    if (jobInfos is null)
    {
        Console.WriteLine($"Failed to get jobs of {memberInfo.Name}.");
    }
    
    members.Add(new Member(int.Parse(memberInfo.Id), memberInfo, jobInfos));
    Console.Write($"\rGet members infos ({members.Count}/{membersInfos.Count})...");
});
Console.WriteLine();

string jobsFile = Path.Combine(Environment.CurrentDirectory, $"{freeCompanyName}.csv");
Console.WriteLine($"""Write "{jobsFile}"...""");

ClassJob[] jobs = new ClassJob[]
{
    // Tanks
    ClassJob.Paladin,
    ClassJob.Warrior,
    ClassJob.DarkKnight,
    ClassJob.Gunbreaker,
    
    // Healers
    ClassJob.WhiteMage,
    ClassJob.Scholar,
    ClassJob.Astrologian,
    ClassJob.Sage,
    
    // Melee DPS
    ClassJob.Monk,
    ClassJob.Dragoon,
    ClassJob.Ninja,
    ClassJob.Samurai,
    ClassJob.Reaper,
    ClassJob.Viper,
    
    // Distance DPS
    ClassJob.Bard,
    ClassJob.Machinist,
    ClassJob.Dancer,
    
    // Magical DPS
    ClassJob.BlackMage,
    ClassJob.Summoner,
    ClassJob.RedMage,
    ClassJob.Pictomancer,
    ClassJob.BlueMage,
    
    // Crafting
    ClassJob.Carpenter,
    ClassJob.Blacksmith,
    ClassJob.Armorer,
    ClassJob.Goldsmith,
    ClassJob.Leatherworker,
    ClassJob.Weaver,
    ClassJob.Alchemist,
    ClassJob.Culinarian,
    
    // Gathering
    ClassJob.Miner,
    ClassJob.Botanist,
    ClassJob.Fisher
};

// Write CSV header

await using StreamWriter writer = File.CreateText(jobsFile);

writer.Write("CharacterID");
writer.Write(",");
writer.Write("CharacterName");
writer.Write(",");
writer.Write("LastUpdate");
writer.Write(",");
writer.Write("FreeCompanyRank");
writer.Write(",");
writer.Write("GrandCompanyRank");
foreach (ClassJob classJob in jobs)
{
    writer.Write(",");
    writer.Write(classJob.ToString());
}
writer.WriteLine();

// Write info of each character in CSV

foreach (Member member in members.OrderBy(x => x.Id))
{
    writer.Write(member.MemberInfo.Id);
    writer.Write(",");
    writer.Write(member.MemberInfo.Name);
    writer.Write(",");
    writer.Write(dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
    writer.Write(",");
    writer.Write(member.MemberInfo.FreeCompanyRank);
    writer.Write(",");
    writer.Write(member.MemberInfo.Rank);
    
    if (member.Jobs is not null)
    {
        try
        {
            bool _ = member.Jobs.Paladin.IsUnlocked;
        }
        catch (FormatException)
        {
            Console.WriteLine($"""Failed to get job levels of "{member.MemberInfo.Name}". Character profile is probably private.""");
            
            writer.WriteLine();
            continue;
        }
        
        foreach (ClassJob job in jobs)
        {
            ClassJobEntry jobInfo = member.Jobs.ClassJobDict[job];
            writer.Write(",");
            writer.Write(jobInfo.IsUnlocked ? jobInfo.Level : string.Empty);
        }
        writer.WriteLine();
    }
}

Console.WriteLine("Done");

internal record Member(int Id, FreeCompanyMembersEntry MemberInfo, CharacterClassJob? Jobs);

public enum Language
{
    jp, na, eu, fr, de
}