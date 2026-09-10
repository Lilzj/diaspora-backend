// ════════════════════════════════════════════════════════════════════════════════════════════════
// The scope-authority seam moved to Diaspora.Contracts in Wave 12, and this file records why so a
// reader following an old reference finds the reason rather than an absence.
//
// `IAutomationScopeAuthority`, `AutomationScopeGrant`, `AutomationAudienceEstimate` and
// `AutomationOwnerKind` now live in `Diaspora.Contracts.Automation.V1.Extensibility`.
//
// Wave 11 declared them here and named CommunityLife, Community and Creator as the modules that would
// implement them. All three compose before this module — 500, 1100 and 1400 against 2100 — and the
// platform's own `No_module_depends_on_a_module_composed_after_it` guardrail forbids any of them from
// referencing this assembly. The seam was unimplementable by every module it was written for, and
// nothing failed to say so: no module tried, the shipped floor answered honestly, and the broadcast
// capability simply stayed granted to nobody.
//
// The implementations stay here. `SelfOnlyScopeAuthority` and `AutomationScopeAuthorityResolver` are
// this module's behaviour rather than its contract, and they are in the file beside this one.
// ════════════════════════════════════════════════════════════════════════════════════════════════

global using AutomationOwnerKind = Diaspora.Contracts.Automation.V1.Extensibility.AutomationOwnerKind;
global using AutomationScopeGrant = Diaspora.Contracts.Automation.V1.Extensibility.AutomationScopeGrant;
global using AutomationAudienceEstimate =
    Diaspora.Contracts.Automation.V1.Extensibility.AutomationAudienceEstimate;
global using IAutomationScopeAuthority =
    Diaspora.Contracts.Automation.V1.Extensibility.IAutomationScopeAuthority;
global using IAutomationConditionSource =
    Diaspora.Contracts.Automation.V1.Extensibility.IAutomationConditionSource;
global using AutomationConditionQuery =
    Diaspora.Contracts.Automation.V1.Extensibility.AutomationConditionQuery;
global using AutomationConditionSources =
    Diaspora.Contracts.Automation.V1.Extensibility.AutomationConditionSources;

// Phase 5. The subject-state seam, declared beside the condition seam and for the same reason: the
// modules that own a subject compose before this one.
global using IAutomationSubjectSource =
    Diaspora.Contracts.Automation.V1.Extensibility.IAutomationSubjectSource;
global using AutomationSubjectQuery =
    Diaspora.Contracts.Automation.V1.Extensibility.AutomationSubjectQuery;
global using AutomationSubjectSnapshot =
    Diaspora.Contracts.Automation.V1.Extensibility.AutomationSubjectSnapshot;
