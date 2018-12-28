# Candler

[Flarum](https://flarum.org/) to [NodeBB](https://nodebb.org/) migration tool

## About

.NET console application for automated migration of discussions/posts from Flarum to NodeBB.

Developed for and tested on Flarum version **0.1.0-beta.7**

## Limits

- Users are **not** migrated, instead a header (*Originally posted by* **{username}**) is inserted into every post body on transformation to keep full-text-search working
- Flarum tags are converted into NodeBB categories under a defined parent category
- Posts are converted in original creation order but are not dated back to original creation date
- Hidden (deleted) posts get skipped
- Post body transformation relies entirely on the magic within Reverse Markdown and doesn't cover all possible formatting quirks that might occur. The quality of the conversion was sufficient for my needs though 😇

## Prerequisites

- Access to MySQL server hosting Flarum database
- Working installation of NodeBB and user with administrative permissions
- Git, Visual Studio and the spirit for going on an adventure! ⚡

## How to use

- **Backup everything!** You know, just in case 😉
- Either use an existing administrative user or create a bot/dummy one just for the import. Give it administrative permissions for the duration of the import in order to avoid running into newbie post rate limits. You can downgrade the users permissions again once the migration is done.
- Install the [Write API plugin](https://github.com/NodeBB/nodebb-plugin-write-api) via plugin manager.
- Configure the Write API plugin
  - Navigate to `https://yourforum.example.org/admin/plugins/write-api`
  - Tick `Require API usage via HTTPS only` (not necessary yet recommended)
  - Under `MASTER TOKENS` click `CREATE TOKEN` and note it
- Clone, compile and configure this solution
  - Adjust `App.config` as follows
    - Adapt `connectionString` to match your database server and credentials
    - Replace the `BearerToken` with the master token you created before
    - Replace the `UserId` with the one of your administrative import user. This is the `uid` value as seen in `https://yourforum.example.org/admin/manage/users`
    - Replace the `ParentCid` with the ID of the (existing) parent category the content shall be imported into. The ID can be grabbed from the URL (e.g. `https://yourforum.example.org/category/`**8**`/archive`)
    - Replace the `BaseUrl` with the location of your NodeBB installation (e.g. `https://yourforum.example.org`)
- Run the `Candler` executable, sit back and watch ☕

## Credits

Most of the heavy-lifting is done by the following awesome 3rd party projects:

- [linq2db](https://github.com/linq2db/linq2db) for accessing Flarum's MySQL database from C#
- [ReverseMarkdown](https://github.com/mysticmind/reversemarkdown-net) for converting HTML post bodies into Markdown
- [Html Agility Pack (HAP)](https://html-agility-pack.net/) for additional clean-up of HTML artefacts
- [Json.NET](https://www.newtonsoft.com/json) for decoding NodeBB REST API responses
- [RestSharp](https://github.com/restsharp/RestSharp) for interaction with NodeBB REST API
- [Serilog](https://serilog.net/) for logging/auditing what's going on
- [Write API plugin](https://github.com/NodeBB/nodebb-plugin-write-api) for pushing content into NodeBB

All I did was reverse the Flarum database schema and glue it all together 🤯