// TypeScript mirrors of the read-model DTOs. The SPA consumes these only — no domain objects,
// no calculations. Every economic amount carries its evidence (ADR-025).

export interface EconomicAmount {
  amount: number;
  currency: string;
  evidenceClass: string;
  source: string;
  methodology: string;
  asOf: string;
  validationState: string;
}

export interface AssetLedgerView {
  assetId: string;
  displayName: string;
  assetType: string;
  registrationStatus: string;
  operationalLifecycleState: string;
  matchConfidence: string;
  isOwnerless: boolean;
  ownerDisplayName?: string | null;
  businessPurpose?: string | null;
  resolutionLinkCount: number;
}

export interface RoiView {
  scope: string;
  cost: EconomicAmount;
  value: EconomicAmount;
  netBenefit: EconomicAmount;
  singlePointRoi: number | null;
  validatedOnlyRoi: number;
  lowRoi: number;
  highRoi: number;
  paybackMonths: number | null;
  singlePointSuppressed: boolean;
  compositeEvidenceClass: string;
  confidenceMix: Record<string, number>;
  asOf: string;
}

export interface ExecutiveEconomicsView {
  totalSpend: EconomicAmount;
  declaredValue: EconomicAmount;
  validatedValue: EconomicAmount;
  validatedToDeclaredRatio: number;
  unattributedCost: EconomicAmount;
  unattributedPercent: number;
  confidenceMix: Record<string, number>;
  asOf: string;
}

export interface GovernanceCaseView {
  caseId: string;
  assetId: string;
  type: string;
  riskTier: string;
  status: string;
  requiredReviewers: string[];
  decisionCount: number;
  outcome?: string | null;
  dueBy: string;
  reuseAction?: string | null;
  slaBreached: boolean;
}

export interface GovernanceDebtView {
  assetId: string;
  debtType: string;
  raisedAt: string;
  isOpen: boolean;
}

export interface CoverageView {
  providersConnected: number;
  assetsKnown: number;
  lastSuccessfulSweep?: string | null;
  coverageNote: string;
  asOf: string;
}

// Resolution & Merge Workbench read models (I4). Display only — no calculation, no domain objects.
export interface IdentifierView {
  system: string;
  identifierType: string;
  value: string;
}

export interface MergeCaseView {
  mergeCaseId: string;
  reason: string;
  confidence: string;
  identifiers: IdentifierView[];
  candidateAssetIds: string[];
  observationRef?: string | null;
  openedAt: string;
}

export interface AliasView {
  system: string;
  identifierType: string;
  value: string;
  provenance: string;
}

export interface ResolutionLinkView {
  linkId: string;
  identifiers: IdentifierView[];
  method: string;
  confidence: string;
  status: string;
  observationRef?: string | null;
  linkedBy: string;
  linkedAt: string;
  supersededByLinkId?: string | null;
}

export interface AssetResolutionView {
  assetId: string;
  displayName: string;
  matchConfidence: string;
  aliases: AliasView[];
  links: ResolutionLinkView[];
}
