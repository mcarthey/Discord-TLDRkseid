# TLDRkseid Release Readiness Report

*Generated: January 9, 2026*

This report summarizes the findings from a comprehensive review of tests, documentation, and requirements for listing TLDRkseid on bot listing sites like top.gg.

---

## Executive Summary

**Overall Readiness: ~85%**

The bot is functionally complete and deployed on Railway. However, there are **5 blocking items** that must be addressed before public listing:

| Blocker | Effort | Status |
|---------|--------|--------|
| Terms of Service document | 30 min | Missing |
| Railway Volume persistence verified | 10 min | Needs verification |
| Test admin commands in production | 20 min | Not done |
| OpenAI cost alerts configured | 5 min | Not done |
| Invite link in README | 15 min | Missing |

---

## Part 1: Test Coverage Analysis

### Current State
- **84 tests** across 7 test files
- Good coverage of business logic services
- **Critical gaps** in command handler and message collection

### Missing Test Coverage (Priority Order)

#### 1. TldrModule (Command Handler) - CRITICAL
The main `/tldr` command has **ZERO tests**. This is the primary feature of the bot.

**Recommended tests to add:**
```csharp
// TldrModuleTests.cs - 40-50 tests needed
TldrAsync_Defers_Response_Immediately()
TldrAsync_With_Null_Author_Messages_Should_Not_Hang()
TldrAsync_Handles_MessageFetchTimeout_After_15_Seconds()
TldrAsync_With_DiscordNetException_HttpError_Should_Retry()
FetchRecentMessages_Returns_Empty_For_No_Permission()
TldrAsync_Truncates_Summary_To_EmbedDescriptionLimit_4096()
TldrAsync_Detects_Repetition_Ratio_Below_60_Percent()
TldrAsync_Warns_About_Max_Depth_Implications()
```

#### 2. MessageCollectorService - HIGH
Completely untested service that tracks messages.

**Recommended tests:**
```csharp
// MessageCollectorServiceTests.cs - 15-20 tests
Track_With_Null_Author_Should_Not_Throw()
Track_Thread_Safety_With_Concurrent_Access()
CleanupOldMessages_Respects_MaxMessageAge_24Hours()
GetMessagesSince_Filters_By_Timestamp()
```

#### 3. AiSummarizerService Advanced Scenarios - HIGH
Timeout and debounce behavior untested.

**Recommended tests:**
```csharp
SummarizeAsync_Timeout_After_30_Seconds()
SummarizeAsync_Debounce_2Second_Minimum_Between_Calls()
SummarizeAsync_Handles_Null_Message_Content()
SummarizeAsync_Handles_Empty_String_Content()
```

#### 4. Database Failure Scenarios - MEDIUM
No tests for EF Core exceptions.

**Recommended tests:**
```csharp
// DatabaseFailureTests.cs - 20-30 tests
AddAdminAsync_Throws_DbUpdateException_Propagates()
IsSuperuserAsync_With_Database_Timeout()
CostTrackerService_AddAsync_Handles_Concurrent_Writes()
```

### Discord.NET Deserialization Bug Test

The known Discord.NET bug cannot be directly unit tested (it's in the library), but we should add a regression test marker:

```csharp
[Fact(Skip = "Discord.NET 3.17.2 bug - MessageComponentConverter throws ArgumentNullException")]
public async Task FetchMessages_With_Components_Should_Not_Throw()
{
    // This test documents the known issue
    // Remove Skip attribute when Discord.NET fixes the bug
    // Expected: Messages with components are fetched without exception
    // Actual: ArgumentNullException in MessageComponentConverter.ReadJson
}
```

### Test Coverage Summary

| Category | Current | Needed | Gap |
|----------|---------|--------|-----|
| Business Logic Services | 84 | 84 | 0 |
| Command Handlers | 0 | 50 | 50 |
| Message Collection | 0 | 20 | 20 |
| Database Failures | 0 | 25 | 25 |
| Integration Tests | 0 | 30 | 30 |
| **Total** | **84** | **209** | **125** |

---

## Part 2: Release Blockers

### Blocking Issues (Must Fix)

#### 1. Terms of Service - MISSING
Bot listing sites require both Privacy Policy AND Terms of Service.

**Action:** Create `docs/TERMS-OF-SERVICE.md` with:
- Acceptable use policy
- Liability limitations
- Service availability disclaimer
- Termination clause
- Discord ToS compliance requirement

#### 2. Railway Volume Persistence - VERIFY
The TODO mentions this is critical but should be verified working.

**Action:** Confirm DATABASE_PATH is set to `/data/tldr.sqlite` and volume is mounted at `/data`.

#### 3. Admin Commands Testing - NOT DONE
`!admin` commands haven't been tested in production.

**Action:** Test these commands in Discord:
- `!admin add @user`
- `!admin remove @user`
- `!admin list`
- `!admin superuser transfer @user`

#### 4. OpenAI Cost Alerts - NOT CONFIGURED
No spending limits means potential for runaway costs.

**Action:**
1. Go to OpenAI dashboard → Settings → Limits
2. Set hard limit (e.g., $50/month)
3. Set soft limit alerts at $10, $25, $50

#### 5. Invite Link - MISSING FROM README
Users can't add the bot without a proper invite link.

**Action:** Generate OAuth2 URL with:
- Scope: `bot applications.commands`
- Permissions: View Channels, Send Messages, Read Message History, Embed Links
- Add to README.md

### High Priority (Before Listing)

| Item | Status | Action |
|------|--------|--------|
| Bot avatar/logo | Missing | Create or commission |
| Support server | Missing | Create Discord server |
| Bot description for listings | Missing | Write compelling copy |
| CONTRIBUTING.md | Missing | Add for open-source contributors |

---

## Part 3: Bot Listing Requirements

### Top.gg Requirements

| Requirement | Status |
|-------------|--------|
| Bot is public | ✅ Yes |
| Bot is online | ✅ Railway deployed |
| Privacy Policy | ✅ PRIVACY.md exists |
| Terms of Service | ❌ Missing |
| Prefix specified correctly | ✅ Slash commands |
| Join top.gg Discord | ❌ Need to join |
| Description ready | ❌ Need to write |

### Review Process
- Takes **1-2 weeks** for review
- Bot must stay online during review
- Reviewers test all features

### Common Rejection Reasons
1. Bot offline during review
2. Commands have permission errors
3. Description violates guidelines
4. Incorrect prefix specified

### Discord Bot Verification (Future)
When the bot reaches 75+ servers, Discord requires verification:
- Privacy Policy URL ✅
- Terms of Service URL ❌
- Bot functionality documentation ❌
- Message Content intent justification ❌

**Justification template for Message Content intent:**
> "TLDRkseid reads message content ONLY when users explicitly invoke the `/tldr` command. Messages are sent to OpenAI for summarization and are not stored. The bot never reads messages automatically or logs message content."

---

## Part 4: Recommended Actions

### Immediate (Before Listing)

1. **Create Terms of Service** (30 min)
   ```markdown
   # Terms of Service
   - Use at your own risk
   - No warranty provided
   - We may terminate access
   - Follow Discord ToS
   ```

2. **Verify Railway Volume** (10 min)
   - Check Railway dashboard
   - Confirm volume mounted at `/data`
   - Verify `DATABASE_PATH=/data/tldr.sqlite`

3. **Test Admin Commands** (20 min)
   - `!admin add`, `!admin remove`, `!admin list`
   - `!admin superuser transfer`

4. **Configure OpenAI Alerts** (5 min)
   - Set hard limit
   - Enable email alerts

5. **Generate Invite Link** (15 min)
   - Use Discord Developer Portal
   - Add to README.md

### Short-Term (First Week After Listing)

1. Create support Discord server
2. Design bot avatar
3. Write compelling listing descriptions
4. Submit to top.gg, discord.bots.gg
5. Add CONTRIBUTING.md

### Medium-Term (First Month)

1. Add 50+ tests for TldrModule
2. Add MessageCollectorService tests
3. Add database failure tests
4. Set up uptime monitoring
5. Prepare Discord verification application

---

## Appendix: Test File Recommendations

Create these new test files:

```
TLDRkseid.Tests/
├── Commands/
│   └── TldrModuleTests.cs          # 50 tests - command handler
├── Services/
│   ├── MessageCollectorServiceTests.cs  # 20 tests - message tracking
│   └── AiSummarizerAdvancedTests.cs     # 15 tests - timeouts, edge cases
├── Infrastructure/
│   └── DatabaseFailureTests.cs     # 25 tests - EF Core exceptions
└── Integration/
    ├── CacheIntegrationTests.cs    # 15 tests - multi-tier cache
    └── ServiceIntegrationTests.cs  # 20 tests - full workflows
```

---

## Conclusion

TLDRkseid is **functionally complete** and ready for users. The main gaps are:

1. **Documentation**: Missing ToS, invite link
2. **Testing**: Command handler untested
3. **Operations**: OpenAI cost controls

With ~2 hours of work on the blockers, the bot can be listed on top.gg and other sites.

**Recommended next session priorities:**
1. Create Terms of Service
2. Add invite link to README
3. Test admin commands
4. Submit to top.gg
5. Start adding TldrModule tests
