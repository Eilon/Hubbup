export interface User {
    id: string;
    url: string;
    name?: string;
    login: string;
    avatarUrl: string;
}

export interface Repository {
    id: string;
    name: string;
    owner: User;
}

export interface Label {
    id: string;
    name: string;
    color: string;
    foreColor: string;
}

export interface Issue {
    id: string;
    isPr: boolean;
    working: boolean;
    url: string;
    number: number;
    repository: Repository;
    title: string;
    author: User;
    milestone?: Milestone;
    createdAt: Date;
    updatedAt: Date;
    workingStartedAt: Date;
    commentCount: number;
    stale: boolean;
    createdTimeAgo: string;
    workingTimeAgo: string;
    assignees: User[];
    labels: Label[];
}

export interface Milestone {
    id: string;
    title: string;
}

export interface GraphQlRateLimit {
    limit: number;
    remaining: number;
    cost: number;
    resetAt: string;
}

export interface RestRateLimit {
    limit: number;
    remaining: number;
    reset: string;
    resetAsUtcEpochSeconds: number;
}

export interface RepoSetIssueResult {
    working: Issue[];
    other: Issue[];
    prs: Issue[];
    graphQlRateLimit: GraphQlRateLimit;
    restRateLimit: RestRateLimit;
    pages: number;
}

export interface DispatchPageData {
    issuesWithoutRepoLabels: Issue[];
    repoLabels: string[]
}
