using System.Net.Http.Json;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;

namespace Jumbo.ApiClient;

public class LpApiClient(HttpClient http)
{
    // Languages
    // The answers that do not depend on the learner (topics, words, pictures) are kept in memory (ApiCache): a page opened a second time
    // does not wait for the network.
    public Task<List<Language>> GetLanguagesAsync() =>
        ApiCache.GetListAsync("languages", ApiCache.Normal, async () => await http.GetFromJsonAsync<List<Language>>("api/languages") ?? []);

    public Task<Language?> GetLanguageByShortNameAsync(string shortName) =>
        ApiCache.GetAsync($"language/{shortName}", ApiCache.Normal, () => http.GetFromJsonAsync<Language>($"api/languages/by-code/{shortName}"));

    // Subjects
    public Task<List<int>> GetBookNumbersAsync() =>
        ApiCache.GetListAsync("book-numbers", ApiCache.Normal, async () => await http.GetFromJsonAsync<List<int>>("api/subjects/book-numbers") ?? []);

    public Task<List<Subject>> GetSubjectsByBookAsync(int bookNumber) =>
        ApiCache.GetListAsync($"subjects/by-book/{bookNumber}", ApiCache.Normal,
            async () => await http.GetFromJsonAsync<List<Subject>>($"api/subjects/by-book/{bookNumber}") ?? []);

    public Task<Subject?> GetSubjectByIdAsync(int id) =>
        ApiCache.GetAsync($"subject/{id}", ApiCache.Normal, () => http.GetFromJsonAsync<Subject>($"api/subjects/{id}"));

    public async Task<string> GetSubjectNameAsync(int subjectId)
    {
        var response = await ApiCache.GetAsync($"subject-name/{subjectId}", ApiCache.Normal,
            () => http.GetFromJsonAsync<SubjectNameResponse>($"api/subjects/{subjectId}/name"));
        return response?.Name ?? string.Empty;
    }

    // Words
    public Task<List<Word>> GetPagedWordsAsync(string filterText, int page, int pageSize, int languageId) =>
        ApiCache.GetListAsync($"words/paged/{languageId}/{pageSize}/{page}/{filterText}", ApiCache.Normal, async () =>
            await http.GetFromJsonAsync<List<Word>>(
                $"api/words?filterText={Uri.EscapeDataString(filterText)}&page={page}&pageSize={pageSize}&languageId={languageId}") ?? []);

    public Task<int> GetWordsCountAsync(int languageId) =>
        ApiCache.GetAsync($"words/count/{languageId}", ApiCache.Normal, () => http.GetFromJsonAsync<int>($"api/words/count?languageId={languageId}"));

    public async Task<List<Word>> GetWordsByLetterAsync(string letter, int languageId, int count) =>
        await http.GetFromJsonAsync<List<Word>>(
            $"api/words/by-letter?letter={Uri.EscapeDataString(letter)}&languageId={languageId}&count={count}") ?? [];

    public async Task<List<Word>> GetWordsBySubjectAndLanguageAsync(int subjectId, int languageId, int count = 12) =>
        await http.GetFromJsonAsync<List<Word>>(
            $"api/words/by-subject?subjectId={subjectId}&languageId={languageId}&count={count}") ?? [];

    public async Task<List<Word>> GetWordsByBookAsync(int bookNumber, int languageId, int count = 12) =>
        await http.GetFromJsonAsync<List<Word>>(
            $"api/words/by-book?bookNumber={bookNumber}&languageId={languageId}&count={count}") ?? [];

    public async Task<int> GetCurrentBookAsync() =>
        await http.GetFromJsonAsync<int>("api/progress/current-book");

    public async Task<List<int>> GetUnlockedBooksAsync() =>
        await http.GetFromJsonAsync<List<int>>("api/progress/books/unlocked") ?? [];

    public async Task<bool> UnlockBookAsync(int bookNumber)
    {
        var response = await http.PostAsync($"api/progress/books/{bookNumber}/unlock", null);
        return response.IsSuccessStatusCode;
    }

    public Task<List<Word>> GetAllWordsBySubjectAndLanguageAsync(int subjectId, int languageId) =>
        ApiCache.GetListAsync($"words/by-subject/all/{subjectId}/{languageId}", ApiCache.Normal, async () =>
            await http.GetFromJsonAsync<List<Word>>($"api/words/by-subject/all?subjectId={subjectId}&languageId={languageId}") ?? []);

    public Task<Word?> GetWordBySvgAndLanguageAsync(int svgImageId, int languageId) =>
        ApiCache.GetAsync($"word/by-svg/{svgImageId}/{languageId}", ApiCache.Long,
            () => http.GetFromJsonAsync<Word>($"api/words/by-svg?svgImageId={svgImageId}&languageId={languageId}"));

    public async Task<List<LetterItem>> GetAlphabetProgressAsync(int studyLanguageId) =>
        await http.GetFromJsonAsync<List<LetterItem>>($"api/words/alphabet-progress?studyLanguageId={studyLanguageId}") ?? [];

    public async Task<List<ExamWordItem>> GetExamWordsAsync(int bookNumber, int studyLanguageId, int transLanguageId, int count) =>
        await http.GetFromJsonAsync<List<ExamWordItem>>(
            $"api/words/exam?bookNumber={bookNumber}&studyLanguageId={studyLanguageId}&transLanguageId={transLanguageId}&count={count}") ?? [];

    public async Task<List<ExamWordItem>> GetTestLevelWordsAsync(int studyLanguageId, int transLanguageId, int count) =>
        await http.GetFromJsonAsync<List<ExamWordItem>>(
            $"api/words/test-level?studyLanguageId={studyLanguageId}&transLanguageId={transLanguageId}&count={count}") ?? [];

    /// <summary>
    /// Fills the cache with the translations and the pictures of the words, several requests at once. A game needs both for every word
    /// (15 words = 30 requests): one after another that is 30 waits of the network, together it is a few. The loops that follow then
    /// find everything in the cache. A failed word is ignored here: the loop asks for it again and reports the error as before.
    /// </summary>
    public async Task PrefetchWordsAsync(IEnumerable<Word> words, int translationLanguageId, bool wholeTopics = true)
    {
        var list = words.ToList();
        if (wholeTopics)
        {
            // one request per topic brings the translations and the pictures of all its words (and puts them in the cache)
            await Task.WhenAll(list.GroupBy(w => (w.SubjectId, w.LanguageId))
                .Select(g => SeedTopicCardsAsync(g.Key.SubjectId, g.Key.LanguageId, translationLanguageId)));
            words = list.Where(w => !IsKnown(w.SvgImageId, translationLanguageId));        // what the topics did not bring: word by word
        }
        using var gate = new SemaphoreSlim(10);
        async Task One(Func<Task> load)
        {
            await gate.WaitAsync();
            try { await load(); } catch { /* the caller asks again */ } finally { gate.Release(); }
        }
        var jobs = new List<Task>();
        foreach (var w in words)
        {
            int svg = w.SvgImageId;
            jobs.Add(One(() => GetWordBySvgAndLanguageAsync(svg, translationLanguageId)));
            jobs.Add(One(() => GetSvgByIdAsync(svg)));
        }
        await Task.WhenAll(jobs);
    }

    private static bool IsKnown(int svgImageId, int translationLanguageId) =>
        ApiCache.Contains($"word/by-svg/{svgImageId}/{translationLanguageId}") && ApiCache.Contains($"svg/{svgImageId}");

    private async Task SeedTopicCardsAsync(int subjectId, int languageId, int translationLanguageId)
    {
        try
        {
            var cards = await ApiCache.GetListAsync($"cards/{subjectId}/{languageId}/{translationLanguageId}", ApiCache.Normal, async () =>
                await http.GetFromJsonAsync<List<WordCardDto>>(
                    $"api/words/by-subject/cards?subjectId={subjectId}&languageId={languageId}&translationLanguageId={translationLanguageId}") ?? []);
            foreach (var c in cards)
            {
                if (c.SvgImageId <= 0) continue;
                if (!string.IsNullOrEmpty(c.Svg))
                    ApiCache.Seed($"svg/{c.SvgImageId}", ApiCache.Long, new SvgImageResponse { Svg = c.Svg });
                if (c.TranslationWordId > 0 && c.TranslationWord is not null)
                    ApiCache.Seed($"word/by-svg/{c.SvgImageId}/{translationLanguageId}", ApiCache.Long, (Word?)new Word
                    {
                        WordId = c.TranslationWordId, SubjectId = c.SubjectId, LanguageId = translationLanguageId, SvgImageId = c.SvgImageId,
                        WordName = c.TranslationWord, Example = c.TranslationExample
                    });
            }
        }
        catch { /* the words that are missing are asked one by one */ }
    }

    // Svg images
    public async Task<string> GetSvgByIdAsync(int id)
    {
        var response = await ApiCache.GetAsync($"svg/{id}", ApiCache.Long, () => http.GetFromJsonAsync<SvgImageResponse>($"api/svg-images/{id}"));
        return response?.Svg ?? string.Empty;
    }

    // Awards
    public async Task<List<AwardItem>> GetAwardsAsync(int languageId) =>
        await http.GetFromJsonAsync<List<AwardItem>>($"api/awards?languageId={languageId}") ?? [];

    /// <summary>The awards earned since the last call (each is returned once).</summary>
    public async Task<List<AwardItem>> GetNewAwardsAsync(int languageId) =>
        await http.GetFromJsonAsync<List<AwardItem>>($"api/awards/new?languageId={languageId}") ?? [];

    // Market
    public async Task<List<MarketItem>> GetMarketItemsAsync(int languageId) =>
        await http.GetFromJsonAsync<List<MarketItem>>($"api/market?languageId={languageId}") ?? [];

    /// <summary>Puts a thing the learner owns on.</summary>
    public async Task<PurchaseResponse?> EquipAsync(int marketId)
    {
        var response = await http.PostAsync($"api/market/{marketId}/equip", null);
        await EnsureOkAsync(response);
        return await response.Content.ReadFromJsonAsync<PurchaseResponse>();
    }

    /// <summary>Takes a worn thing off (kind: frame | avatar | badge | theme | pet).</summary>
    public async Task<PurchaseResponse?> UnequipAsync(string kind)
    {
        var response = await http.PostAsync($"api/market/unequip/{kind}", null);
        await EnsureOkAsync(response);
        return await response.Content.ReadFromJsonAsync<PurchaseResponse>();
    }

    public async Task<PurchaseResponse?> PurchaseAsync(int marketId)
    {
        var response = await http.PostAsync($"api/market/{marketId}/purchase", null);
        await EnsureOkAsync(response);          // a refusal comes with its code: NOT_ENOUGH, OWNED, LIVES_FULL, LIMIT, WOULD_BLOCK
        return await response.Content.ReadFromJsonAsync<PurchaseResponse>();
    }

    // Users
    public async Task<RegisterUserResponse?> RegisterAsync(RegisterUserRequest request)
    {
        var response = await http.PostAsJsonAsync("api/users/register", request);
        if (!response.IsSuccessStatusCode)
            throw new Exception(await ExtractErrorMessageAsync(response));
        return await response.Content.ReadFromJsonAsync<RegisterUserResponse>();
    }

    /// <summary>Null for a wrong login or password; throws <see cref="LoginBlockedException"/> when the account is locked for a while after many wrong passwords.</summary>
    public async Task<RegisterUserResponse?> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("api/users/login", request);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            int seconds = 300;
            try { seconds = response.Headers.RetryAfter?.Delta is { } d ? (int)d.TotalSeconds : seconds; } catch { }
            throw new LoginBlockedException(Math.Max(1, (int)Math.Ceiling(seconds / 60.0)));
        }
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<RegisterUserResponse>();
    }

    public async Task ChangeCredentialsAsync(ChangeCredentialsRequest request)
    {
        var response = await http.PutAsJsonAsync("api/users/me/credentials", request);
        if (!response.IsSuccessStatusCode)
            throw new Exception(await ExtractErrorMessageAsync(response));
    }

    /// <summary>"Subscribe" in the footer of the website. Returns false for an address that is not an address.</summary>
    public async Task<bool> SubscribeAsync(string email, string lang, string? message = null, bool subscribe = true)
    {
        var response = await http.PostAsJsonAsync("api/subscribe", new SubscribeRequest { Email = email, Lang = lang, Message = message, Subscribe = subscribe });
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>What the server says about the versions of the app on a platform (android | ios | windows).</summary>
    public async Task<AppVersionDto?> GetAppVersionAsync(string platform) =>
        await http.GetFromJsonAsync<AppVersionDto>($"api/app/version?platform={Uri.EscapeDataString(platform)}");

    public async Task<List<AppVersionDto>> AdminGetAppVersionsAsync() =>
        await http.GetFromJsonAsync<List<AppVersionDto>>("api/admin/app-versions") ?? [];

    public async Task AdminSetAppVersionAsync(AppVersionDto dto)
    {
        var response = await http.PutAsJsonAsync($"api/admin/app-versions/{dto.Platform}", dto);
        if (!response.IsSuccessStatusCode) throw new Exception(await ExtractErrorMessageAsync(response));
    }

    public async Task<List<FeedbackDto>> AdminGetFeedbackAsync() =>
        await http.GetFromJsonAsync<List<FeedbackDto>>("api/admin/feedback") ?? [];

    // Ads
    /// <summary>The ad to show to the signed-in learner now, or null (not a learner of the free version, nothing is due).</summary>
    public async Task<AdForUserDto?> GetNextAdAsync()
    {
        var response = await http.GetAsync("api/ads/next");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent || !response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AdForUserDto>();
    }

    public async Task<List<AdDto>> AdminGetAdsAsync() => await http.GetFromJsonAsync<List<AdDto>>("api/admin/ads") ?? [];

    public async Task<AdDto?> AdminGetAdAsync(int id) => await http.GetFromJsonAsync<AdDto>($"api/admin/ads/{id}");

    /// <summary>Creates (id 0) or changes an ad; the API answers with an error code (NO_TITLE, NO_TEXT, BAD_IMAGE ...) thrown as the message.</summary>
    public async Task<AdDto?> AdminSaveAdAsync(int id, SaveAdRequest request)
    {
        var response = id == 0 ? await http.PostAsJsonAsync("api/admin/ads", request) : await http.PutAsJsonAsync($"api/admin/ads/{id}", request);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException((await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Error ?? response.StatusCode.ToString());
        return await response.Content.ReadFromJsonAsync<AdDto>();
    }

    public async Task AdminDeleteAdAsync(int id) => (await http.DeleteAsync($"api/admin/ads/{id}")).EnsureSuccessStatusCode();

    public async Task AdminResetAdViewsAsync(int id) => (await http.PostAsync($"api/admin/ads/{id}/reset-views", null)).EnsureSuccessStatusCode();

    // Prices of the versions, balance
    public async Task<List<PlanPriceDto>> GetPlanPricesAsync() =>
        await http.GetFromJsonAsync<List<PlanPriceDto>>("api/plans/prices") ?? [];

    public async Task<List<BalanceTransactionDto>> GetMyBalanceTransactionsAsync() =>
        await http.GetFromJsonAsync<List<BalanceTransactionDto>>("api/plans/transactions") ?? [];

    /// <summary>Buys a version from the balance; the API answers with an error code (NOT_ENOUGH, NO_PRICE, HIGHER_ACTIVE...) that is thrown as the message.</summary>
    public async Task<PurchasePlanResponse?> PurchasePlanAsync(string plan, string period)
    {
        var response = await http.PostAsJsonAsync("api/plans/purchase", new PurchasePlanRequest { Plan = plan, Period = period });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException((await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Error ?? response.StatusCode.ToString());
        return await response.Content.ReadFromJsonAsync<PurchasePlanResponse>();
    }

    public async Task<List<PlanPriceDto>> AdminSetPlanPricesAsync(List<PlanPriceDto> prices)
    {
        var response = await http.PutAsJsonAsync("api/admin/plan-prices", new SetPlanPricesRequest { Prices = prices });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException((await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Error ?? response.StatusCode.ToString());
        return await response.Content.ReadFromJsonAsync<List<PlanPriceDto>>() ?? [];
    }

    public async Task<PlanUserDto?> AdminTopUpAsync(TopUpRequest request)
    {
        var response = await http.PostAsJsonAsync("api/admin/balance/topup", request);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException((await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Error ?? response.StatusCode.ToString());
        return await response.Content.ReadFromJsonAsync<PlanUserDto>();
    }

    public async Task<List<BalanceTransactionDto>> AdminGetBalanceTransactionsAsync() =>
        await http.GetFromJsonAsync<List<BalanceTransactionDto>>("api/admin/balance/transactions") ?? [];

    public async Task<List<PlanUserDto>> AdminGetPlansAsync() =>
        await http.GetFromJsonAsync<List<PlanUserDto>>("api/admin/plans") ?? [];

    /// <summary>Gives a user a version until a date; the API answers with an error code (NO_USER, BAD_DATE) the page shows.</summary>
    public async Task<PlanUserDto?> AdminSetPlanAsync(SetPlanRequest request)
    {
        var response = await http.PutAsJsonAsync("api/admin/plans", request);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException((await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Error ?? response.StatusCode.ToString());
        return await response.Content.ReadFromJsonAsync<PlanUserDto>();
    }

    public async Task<List<SubscriberDto>> AdminGetSubscribersAsync() =>
        await http.GetFromJsonAsync<List<SubscriberDto>>("api/admin/subscribers") ?? [];

    // Deletes the account for good (the password is asked once more).
    public async Task DeleteAccountAsync(DeleteAccountRequest request)
    {
        var response = await http.PostAsJsonAsync("api/users/me/delete", request);
        if (!response.IsSuccessStatusCode)
            throw new Exception(await ExtractErrorMessageAsync(response));
    }

    private static async Task<string> ExtractErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            if (!string.IsNullOrWhiteSpace(body?.Error)) return body.Error;
        }
        catch { /* body wasn't JSON */ }
        return $"Запрос не выполнен ({(int)response.StatusCode}).";
    }

    private class ErrorResponse
    {
        public string? Error { get; set; }
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        var response = await http.GetAsync("api/users/me");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<User>();
    }

    public async Task<User?> UpdateCurrentUserAsync(UpdateUserRequest request)
    {
        var response = await http.PutAsJsonAsync("api/users/me", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<User>();
    }

    public async Task<User?> SetAvatarAsync(string avatarSvg)
    {
        var response = await http.PutAsJsonAsync("api/users/me/avatar", new SetAvatarRequest { AvatarSvg = avatarSvg });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<User>();
    }

    // Progress
    public async Task<List<UsersWord>> GetUserWordsAsync() =>
        await http.GetFromJsonAsync<List<UsersWord>>("api/progress/words") ?? [];

    public async Task EnsureWordsExistAsync(IEnumerable<int> wordIds)
    {
        var response = await http.PostAsJsonAsync("api/progress/words/ensure", new EnsureWordsRequest { WordIds = wordIds.ToList() });
        response.EnsureSuccessStatusCode();
    }

    // ── staff requests / admin ────────────────────────────────────────────
    public async Task<StaffRequestDto?> GetMyStaffRequestAsync()
    {
        var response = await http.GetAsync("api/staff-requests/mine");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<StaffRequestDto>();
    }

    public async Task<StaffRequestDto?> CreateStaffRequestAsync(CreateStaffRequest request)
    {
        var response = await http.PostAsJsonAsync("api/staff-requests", request);
        await EnsureOkAsync(response);
        return await response.Content.ReadFromJsonAsync<StaffRequestDto>();
    }

    public async Task<List<SchoolDto>> GetVerifiedSchoolsAsync(string region) =>
        await http.GetFromJsonAsync<List<SchoolDto>>($"api/schools?region={Uri.EscapeDataString(region)}") ?? [];

    public async Task<List<StaffRequestDto>> AdminGetStaffRequestsAsync(string status) =>
        await http.GetFromJsonAsync<List<StaffRequestDto>>($"api/admin/staff-requests?status={Uri.EscapeDataString(status)}") ?? [];

    public async Task AdminApproveStaffRequestAsync(int id, ApproveStaffRequest body)
    {
        var response = await http.PostAsJsonAsync($"api/admin/staff-requests/{id}/approve", body);
        await EnsureOkAsync(response);
    }

    public async Task AdminRejectStaffRequestAsync(int id, RejectStaffRequest body)
    {
        var response = await http.PostAsJsonAsync($"api/admin/staff-requests/{id}/reject", body);
        await EnsureOkAsync(response);
    }

    public async Task<List<SchoolDto>> AdminGetSchoolsAsync(string? region = null) =>
        await http.GetFromJsonAsync<List<SchoolDto>>("api/admin/schools" + (string.IsNullOrEmpty(region) ? "" : $"?region={Uri.EscapeDataString(region)}")) ?? [];

    public async Task AdminCreateSchoolAsync(CreateSchoolRequest body)
    {
        var response = await http.PostAsJsonAsync("api/admin/schools", body);
        await EnsureOkAsync(response);
    }

    public async Task AdminSetSchoolVerifiedAsync(int id, bool verified)
    {
        var response = await http.PutAsJsonAsync($"api/admin/schools/{id}/verified", new SetVerifiedRequest { IsVerified = verified });
        await EnsureOkAsync(response);
    }

    // ── classes / teacher cabinet / exam results ──────────────────────────
    public async Task SubmitExamResultAsync(SubmitExamResultRequest request)
    {
        var response = await http.PostAsJsonAsync("api/exam-results", request);
        await EnsureOkAsync(response);
    }

    // A pupil enters the 6-digit code of a teacher's exam. Throws with the API's error code (CODE_INVALID, CODE_EXPIRED, ...).
    public async Task<JoinTeacherExamResponse?> JoinTeacherExamAsync(string code)
    {
        var response = await http.PostAsJsonAsync("api/exam-results/join", new JoinTeacherExamRequest { Code = code });
        await EnsureOkAsync(response);
        return await response.Content.ReadFromJsonAsync<JoinTeacherExamResponse>();
    }

    // The rating of the teacher's own pupils (classId 0 = all of them).
    public async Task<RatingDto?> TeacherGetRatingAsync(int classId, string period)
    {
        var response = await http.GetAsync($"api/teacher/rating?classId={classId}&period={Uri.EscapeDataString(period)}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<RatingDto>();
    }

    public async Task<TeacherExamSummaryDto?> TeacherCreateExamAsync(int wordCount)
    {
        var response = await http.PostAsJsonAsync("api/teacher/exams", new CreateTeacherExamRequest { WordCount = wordCount });
        await EnsureOkAsync(response);
        return await response.Content.ReadFromJsonAsync<TeacherExamSummaryDto>();
    }

    public async Task<List<TeacherExamSummaryDto>> TeacherGetExamsAsync() =>
        await http.GetFromJsonAsync<List<TeacherExamSummaryDto>>("api/teacher/exams") ?? [];

    public async Task<List<TeacherExamResultDto>> TeacherGetAllExamResultsAsync(int? classId = null) =>
        await http.GetFromJsonAsync<List<TeacherExamResultDto>>("api/teacher/exams/results" + (classId is { } c ? $"?classId={c}" : "")) ?? [];

    public async Task<List<ExamTeamDto>> TeacherGetExamTeamsAsync() =>
        await http.GetFromJsonAsync<List<ExamTeamDto>>("api/teacher/exams/teams") ?? [];

    public async Task<ExamTeamReportDto?> TeacherGetExamTeamReportAsync(int classId)
    {
        var response = await http.GetAsync($"api/teacher/exams/teams/{classId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureOkAsync(response);
        return await response.Content.ReadFromJsonAsync<ExamTeamReportDto>();
    }

    public async Task<TeacherExamDetailDto?> TeacherGetExamAsync(int id)
    {
        var response = await http.GetAsync($"api/teacher/exams/{id}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureOkAsync(response);
        return await response.Content.ReadFromJsonAsync<TeacherExamDetailDto>();
    }

    public async Task<List<MyExamBookResultDto>> GetMyExamResultsAsync() =>
        await http.GetFromJsonAsync<List<MyExamBookResultDto>>("api/exam-results/mine") ?? [];

    // Pick lists of the registration wizard / profile (public endpoints).
    public async Task<List<SchoolDto>> GetDirectorySchoolsAsync(string region) =>
        await http.GetFromJsonAsync<List<SchoolDto>>($"api/directory/schools?region={Uri.EscapeDataString(region)}") ?? [];

    public async Task<List<TeamOptionDto>> GetDirectoryTeamsAsync(int schoolId) =>
        await http.GetFromJsonAsync<List<TeamOptionDto>>($"api/directory/schools/{schoolId}/teams") ?? [];

    public async Task<MyTeamDto?> GetMyTeamAsync()
    {
        var response = await http.GetAsync("api/classes/mine");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<MyTeamDto>();
    }

    public async Task<MyTeamDto?> SelectTeamAsync(int classId)
    {
        var response = await http.PostAsJsonAsync("api/classes/select", new SelectTeamRequest { ClassId = classId });
        await EnsureOkAsync(response);
        return await response.Content.ReadFromJsonAsync<MyTeamDto>();
    }

    public async Task LeaveTeamAsync()
    {
        var response = await http.PostAsync("api/classes/leave", null);
        await EnsureOkAsync(response);
    }

    public async Task<List<ClassDto>> TeacherGetClassesAsync(bool includeArchived = false) =>
        await http.GetFromJsonAsync<List<ClassDto>>($"api/teacher/classes?includeArchived={includeArchived.ToString().ToLowerInvariant()}") ?? [];

    public async Task<ClassDto?> TeacherCreateClassAsync(string name)
    {
        var response = await http.PostAsJsonAsync("api/teacher/classes", new CreateClassRequest { Name = name });
        await EnsureOkAsync(response);
        return await response.Content.ReadFromJsonAsync<ClassDto>();
    }

    public async Task<ClassDetailDto?> TeacherGetClassAsync(int id)
    {
        var response = await http.GetAsync($"api/teacher/classes/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ClassDetailDto>();
    }

    public async Task<StudentProgressDto?> TeacherGetStudentProgressAsync(int classId, int userId)
    {
        var response = await http.GetAsync($"api/teacher/classes/{classId}/students/{userId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<StudentProgressDto>();
    }

    public async Task<ClassDto?> TeacherSetArchivedAsync(int id, bool archived)
    {
        var response = await http.PutAsJsonAsync($"api/teacher/classes/{id}/archived", new SetArchivedRequest { IsArchived = archived });
        await EnsureOkAsync(response);
        return await response.Content.ReadFromJsonAsync<ClassDto>();
    }

    public async Task<TeamReportDto?> TeacherGetTeamReportAsync(int classId, string period)
    {
        var response = await http.GetAsync($"api/teacher/classes/{classId}/report?period={Uri.EscapeDataString(period)}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<TeamReportDto>();
    }

    public async Task<ReportLinkDto?> TeacherGetSchoolReportLinkAsync()
    {
        var response = await http.GetAsync("api/teacher/school-report-link");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ReportLinkDto>();
    }

    public async Task<ReportLinkDto?> TeacherCreateSchoolReportLinkAsync()
    {
        var response = await http.PostAsync("api/teacher/school-report-link", null);
        await EnsureOkAsync(response);
        return await response.Content.ReadFromJsonAsync<ReportLinkDto>();
    }

    public async Task TeacherRevokeSchoolReportLinkAsync()
    {
        var response = await http.DeleteAsync("api/teacher/school-report-link");
        await EnsureOkAsync(response);
    }

    // A pupil connects their own Telegram to the bot (settings of the app / site).
    public async Task<TelegramStatusDto?> GetMyTelegramAsync()
    {
        var response = await http.GetAsync("api/users/me/telegram");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<TelegramStatusDto>();
    }

    public async Task<TelegramConnectionDto?> CreateMyTelegramLinkAsync()
    {
        var response = await http.PostAsync("api/users/me/telegram/link", null);
        await EnsureOkAsync(response);
        return await response.Content.ReadFromJsonAsync<TelegramConnectionDto>();
    }

    public async Task RemoveMyTelegramAsync(int id)
    {
        var response = await http.DeleteAsync($"api/users/me/telegram/{id}");
        await EnsureOkAsync(response);
    }

    // Telegram bot: who gets the weekly school report (the director never logs in).
    public async Task<TelegramStatusDto?> TeacherGetTelegramAsync()
    {
        var response = await http.GetAsync("api/teacher/telegram");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<TelegramStatusDto>();
    }

    // mine = true: the teacher's own account (pupil requests, reminders); false: a report recipient (the director).
    public async Task<TelegramConnectionDto?> TeacherCreateTelegramLinkAsync(bool mine = false)
    {
        var response = await http.PostAsync($"api/teacher/telegram/link?mine={(mine ? "true" : "false")}", null);
        await EnsureOkAsync(response);
        return await response.Content.ReadFromJsonAsync<TelegramConnectionDto>();
    }

    public async Task TeacherRemoveTelegramAsync(int id)
    {
        var response = await http.DeleteAsync($"api/teacher/telegram/{id}");
        await EnsureOkAsync(response);
    }

    // Public: the director's link. null = unknown or revoked token.
    public async Task<SchoolReportDto?> GetPublicSchoolReportAsync(string token, string period)
    {
        var response = await http.GetAsync($"api/reports/{Uri.EscapeDataString(token)}?period={Uri.EscapeDataString(period)}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<SchoolReportDto>();
    }

    public async Task TeacherApproveStudentAsync(int classId, int userId)
    {
        var response = await http.PostAsync($"api/teacher/classes/{classId}/students/{userId}/approve", null);
        await EnsureOkAsync(response);
    }

    public async Task TeacherApproveAllAsync(int classId)
    {
        var response = await http.PostAsync($"api/teacher/classes/{classId}/approve-all", null);
        await EnsureOkAsync(response);
    }

    public async Task TeacherRemoveStudentAsync(int classId, int userId)
    {
        var response = await http.DeleteAsync($"api/teacher/classes/{classId}/students/{userId}");
        await EnsureOkAsync(response);
    }

    // Throws with the server's {"error": "..."} text (e.g. PENDING_EXISTS) so the UI can map it to a message.
    private static async Task EnsureOkAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        string? error = null;
        try { error = (await response.Content.ReadFromJsonAsync<ErrorBody>())?.Error; } catch { }
        throw new InvalidOperationException(error ?? response.ReasonPhrase ?? response.StatusCode.ToString());
    }

    private sealed class ErrorBody { public string? Error { get; set; } }

    // true = the login is free. On a network/server error the caller should let registration proceed (the server re-checks).
    public async Task<bool> IsUsernameAvailableAsync(string username)
    {
        var response = await http.GetAsync($"api/users/username-available?username={Uri.EscapeDataString(username)}");
        if (!response.IsSuccessStatusCode) return true;
        return await response.Content.ReadFromJsonAsync<bool>();
    }

    // Public top list (works without a token): the website's home page uses it for visitors.
    public async Task<RatingDto?> GetTopRatingAsync(string period, int take = 10)
    {
        var response = await http.GetAsync($"api/rating/top?period={Uri.EscapeDataString(period)}&take={take}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<RatingDto>();
    }

    public async Task<RatingDto?> GetRatingAsync(string scope, string period, int take = 50)
    {
        var response = await http.GetAsync($"api/rating?scope={Uri.EscapeDataString(scope)}&period={Uri.EscapeDataString(period)}&take={take}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<RatingDto>();
    }

    public async Task<DailyProgressDto?> GetDailyProgressAsync()
    {
        var response = await http.GetAsync("api/progress/daily");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<DailyProgressDto>();
    }

    public async Task<DailyProgressDto?> SetDailyGoalAsync(int goalWords)
    {
        var response = await http.PutAsJsonAsync("api/users/me/daily-goal", new SetDailyGoalRequest { GoalWords = goalWords });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DailyProgressDto>();
    }

    public async Task IncrementWordRepetitionAsync(int wordId)
    {
        var response = await http.PostAsync($"api/progress/words/{wordId}/increment", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<UserSubjectProgress>> GetUserSubjectProgressAsync() =>
        await http.GetFromJsonAsync<List<UserSubjectProgress>>("api/progress/subjects") ?? [];

    public async Task<UserSubjectProgress?> GetUserSubjectProgressAsync(int subjectId)
    {
        var response = await http.GetAsync($"api/progress/subjects/{subjectId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<UserSubjectProgress>();
    }

    /// <summary>A won game. The server gives the LP for it and answers with the learner's numbers (give them to LivesService.Apply).</summary>
    public async Task<LivesStateDto?> MarkGameWonAsync(int subjectId, string gameType)
    {
        var response = await http.PostAsJsonAsync($"api/progress/subjects/{subjectId}/win", new MarkGameWonRequest { GameType = gameType });
        response.EnsureSuccessStatusCode();
        try { return await response.Content.ReadFromJsonAsync<LivesStateDto>(); }
        catch { return null; }          // an older server answers with nothing
    }

    // Lives and LP: the server counts them
    public async Task<LivesStateDto?> GetLivesAsync() => await http.GetFromJsonAsync<LivesStateDto>("api/lives");

    public async Task<LivesStateDto?> ReportMistakeAsync(bool livesOnly)
    {
        var response = await http.PostAsJsonAsync("api/lives/mistake", new MistakeRequest { LivesOnly = livesOnly });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LivesStateDto>();
    }

    // QR login
    public async Task<QrSessionResponse?> CreateQrLoginSessionAsync()
    {
        var response = await http.PostAsync("api/auth/qr/create", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QrSessionResponse>();
    }

    public async Task<QrStatusResponse?> GetQrLoginStatusAsync(Guid sessionId) =>
        await http.GetFromJsonAsync<QrStatusResponse>($"api/auth/qr/{sessionId}/status");

    // One-time code that opens the website already signed in (see AuthController.CreateWebLink).
    public async Task<Guid?> CreateWebLinkAsync()
    {
        var response = await http.PostAsync("api/auth/web-link", null);
        if (!response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<QrSessionResponse>())?.SessionId;
    }

    // Website side: trades the code for the access token (null when the code is wrong, used or expired).
    public async Task<string?> RedeemWebLinkAsync(Guid code)
    {
        var response = await http.PostAsync($"api/auth/web-link/{code}/redeem", null);
        if (!response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<QrStatusResponse>())?.AccessToken;
    }

    public async Task ConfirmQrLoginAsync(Guid sessionId)
    {
        var response = await http.PostAsync($"api/auth/qr/{sessionId}/confirm", null);
        response.EnsureSuccessStatusCode();
    }
}
