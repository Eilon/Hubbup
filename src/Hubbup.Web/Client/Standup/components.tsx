import * as React from 'react';

import * as Data from '../data';

const fetchSettings: RequestInit = {
    credentials: 'same-origin',
    headers: {
        ['X-Requested-With']: 'XMLHttpRequest'
    },
};

class LoadingPanel extends React.Component<undefined, undefined> {
    render() {
        return <div className="progress">
            <div className="progress-bar progress-bar-striped active" style={{ width: '100%' }}>
                <span className="sr-only">Loading...</span>
            </div>
        </div>
    }
};

class ErrorPanel extends React.Component<{ message: string }, undefined> {
    render() {
        return <div className="alert alert-danger">{this.props.message}</div>;
    }
};

class UserIcon extends React.Component<{ user: Data.User }, undefined> {
    render() {
        return <a href={this.props.user.url}>
            <img style={{ margin: '0.1em' }} height="32" width="32" src={this.props.user.avatarUrl} title={this.props.user.login} alt={this.props.user.login} />
        </a>;
    }
}

class IssueUserView extends React.Component<{ issue: Data.Issue }, undefined> {
    render() {
        const issue = this.props.issue;
        const issueLink = <span className="issue-link">
            <a href={issue.url}>
                #{issue.number}
            </a>
        </span>;

        const style = { whiteSpace: 'nowrap' };
        if (issue.assignees && issue.assignees[0] && issue.assignees[0].id !== issue.author.id) {
            return <div className="col-md-3" style={style}>
                <UserIcon user={issue.author} />
                <span className="glyphicon glyphicon-arrow-right" />
                <UserIcon user={issue.assignees[0]} />
                {issueLink}
            </div>
        }
        else {
            return <div className="col-md-3" style={style}>
                <UserIcon user={issue.author} />
                {issueLink}
            </div>
        }
    }
}

class LabelView extends React.Component<{ label: Data.Label }, undefined> {
    render() {
        const style = {
            backgroundColor: `#${this.props.label.color}`,
            color: `#${this.props.label.foreColor}`
        };
        return <span className="label" style={style}>
            {this.props.label.name}
        </span>;
    }
}

class AgeBadge extends React.Component<{ date: Date, timeAgo: string, prefix: string, stale: boolean }, undefined> {
    render() {
        const cssClass =  ? `badge-pad badge pull-right${this.props.stale ? ' stale': ''}`;
        return <span className={cssClass} title={`${this.props.prefix} on ${this.props.date}`}>
            {this.props.prefix} {this.props.timeAgo}
        </span>;
    }
}

class Badge extends React.Component<{ title: string }, undefined> {
    render() {
        return <span className="badge pull-right badge-pad">{this.props.title}</span>;
    }
}

class IssueView extends React.Component<{ issue: Data.Issue }, undefined> {
    render() {
        const issue = this.props.issue;

        let milestoneBadge;
        if (!issue.isPr) {
            const title = issue.milestone ? issue.milestone.title : '< No Milestone >';
            milestoneBadge = <Badge title={title} />;
        }

        return <li className="list-group-item">
            <div className="row">
                <IssueUserView issue={issue} />
                <div className="col-md-9">
                    <div>
                        <span className="repo-name">
                            {issue.repository.owner.login}/{issue.repository.name}
                        </span>
                        <strong>{issue.title}</strong>
                    </div>

                    {issue.labels.map(label => <LabelView key={label.id} label={label} />)}

                    {issue.working ?
                        <AgeBadge date={issue.workingStartedAt} timeAgo={issue.workingTimeAgo} prefix="Started working" stale={issue.stale} /> :
                        <AgeBadge date={issue.createdAt} timeAgo={issue.createdTimeAgo} prefix="Opened" stale={issue.stale} />}

                    {milestoneBadge}
                </div>
            </div>
        </li>;
    }
}

class IssueList extends React.Component<{ id: string, issues: Data.Issue[], emptyText: string, title: string, collapsable?: boolean }, undefined> {
    render() {
        let content;
        let collapser;
        if (this.props.issues.length > 0) {
            content = <ul className="list-group">
                {this.props.issues.map(issue => <IssueView key={issue.id} issue={issue} />)}
            </ul>;
            if (this.props.collapsable) {
                collapser = <button className="btn btn-xs btn-info chevronButton" type="button" data-toggle="collapse" data-target={`#collapse-otherissues-${this.props.id}`}>
                    <span id="chevronGlyph" className="glyphicon glyphicon-chevron-right"></span>
                    {' ' /* https://facebook.github.io/react/blog/2014/02/20/react-v0.9.html#jsx-whitespace */}
                    Show/hide
                </button>;
                content = <div className="collapse" id={`collapse-otherissues-${this.props.id}`} style={{ marginTop: '10px' }}>
                    {content}
                </div>;
            }
        }
        else {
            content = <div className="alert alert-info">{this.props.emptyText}</div>;
        }

        return <div>
            <h4>
                {this.props.title}
                {' ' /* https://facebook.github.io/react/blog/2014/02/20/react-v0.9.html#jsx-whitespace */}
                <span className="badge">{this.props.issues.length}</span>
                {' ' /* https://facebook.github.io/react/blog/2014/02/20/react-v0.9.html#jsx-whitespace */}
                {collapser}
            </h4>
            {content}
        </div>;
    }
}

interface PersonProps {
    baseUrl: string,
    repoSet: string,
    login: string,
    environment: string,
    updateRateLimit: (graphQl: { cost: number, remaining: number, resetAt: string }, rest: { remaining: number, reset: string }) => void;
}

class Person extends React.Component<PersonProps, { loading: boolean, error: string, data: Data.RepoSetIssueResult }> {
    constructor(props: PersonProps) {
        super(props);
        this.state = {
            loading: true,
            error: '',
            data: null
        };
    }

    componentDidMount() {
        // Can't await this here, but that's OK, it will call setState eventually.
        this.reload();
    }

    async reload() {
        // This doesn't immediately clear the loading widget, but it will when the function returns and we re-render.
        this.setState({
            loading: true,
            error: '',
            data: null
        });

        const resp = await fetch(`${this.props.baseUrl}api/repoSets/${this.props.repoSet}/issues/${this.props.login}`, fetchSettings)
        if (resp.status < 200 || resp.status > 299) {
            this.setState({
                loading: false,
                error: `Unexpected response fetching issues: ${resp.status} ${resp.statusText}`,
                data: null
            });
            return;
        }
        else {
            const data = (await resp.json()) as Data.RepoSetIssueResult;

            // Update rate limit info
            this.props.updateRateLimit(data.graphQlRateLimit, data.restRateLimit);

            // Update my state
            this.setState({
                loading: false,
                error: '',
                data: data
            });
        }
    }

    render() {
        let content;
        let rateLimitDisplay;

        const headerBadgeStyles = {
            marginLeft: '0.5em',
            marginTop: '-5px'
        };

        if (this.state.loading) {
            content = <div className="panel-body">
                <LoadingPanel />
            </div>;
        }
        else if (this.state.error) {
            content = <div className="panel-body">
                <ErrorPanel message={this.state.error} />
            </div>;
        }
        else if (this.state.data) {
            content = <div className="panel-body">
                <IssueList id={this.props.login} issues={this.state.data.working} emptyText="Not working on any assigned issues" title="Working on issues" />
                <IssueList id={this.props.login} issues={this.state.data.prs} emptyText="No pull requests created or assigneds" title="Pull requests" />
                <IssueList id={this.props.login} issues={this.state.data.other} emptyText="No other assigned issues" title="Other assigned issues" collapsable={true} />
            </div>

            if (this.props.environment === 'Development') {
                rateLimitDisplay = <span>
                    <span className="badge" style={headerBadgeStyles}>Rate Limit Cost: {this.state.data.graphQlRateLimit.cost}</span>
                    <span className="badge" style={headerBadgeStyles}>Page Count: {this.state.data.pages}</span>
                </span>;
            }
        }

        return <div className="panel panel-primary">
            <div className="panel-heading">
                <div className="pull-right">
                    {rateLimitDisplay}
                    <button className="btn btn-info btn-xs" style={headerBadgeStyles} onClick={() => this.reload()}>
                        <span className="glyphicon glyphicon-refresh" />
                    </button>
                </div>
                <h3 className="panel-title">
                    <a href={`https://github.com/issues?utf8=%E2%9C%93&q=is%3Aopen+assignee%3A${this.props.login}`}>{this.props.login}</a>
                </h3>
            </div>
            {content}
        </div>;
    }
};

interface PageProps {
    repoSet: string,
    baseUrl: string,
    environment: string
}

interface PageState {
    loading: boolean,
    error: string,
    people: string[],
    rateLimit: {
        graphQl: { cost: number, remaining: number, resetAt: string },
        rest: { remaining: number, reset: string },
        sinceReload: number
    }
}

export class Page extends React.Component<PageProps, PageState> {
    constructor(props: PageProps) {
        super(props);
        this.state = {
            loading: true,
            error: '',
            people: [],
            rateLimit: {
                graphQl: null,
                rest: null,
                sinceReload: 0
            }
        };
    }

    async componentDidMount() {
        // Fetch the list of people
        const newState = { ... this.state };
        const resp = await fetch(`${this.props.baseUrl}api/repoSets/${this.props.repoSet}/people`, fetchSettings);
        if (resp.status < 200 || resp.status > 299) {
            newState.loading = false;
            newState.error = `Unexpected response from server: ${resp.status} ${resp.statusText}`;
        }
        else {
            newState.loading = false;
            newState.error = '';
            newState.people = (await resp.json()) as string[];
        }

        this.setState(newState);
    }

    updateRateLimit(graphQl: { cost: number, remaining: number, resetAt: string }, rest: { remaining: number, reset: string }) {
        var newState = { ... this.state };

        if (graphQl) {
            newState.rateLimit.graphQl = graphQl;
            newState.rateLimit.sinceReload += graphQl.cost;
        }
        if (rest) {
            newState.rateLimit.rest = rest;
        }

        this.setState(newState);
    }

    render() {
        if (this.state.loading) {
            return <LoadingPanel />;
        }
        else if (this.state.error) {
            return <ErrorPanel message={this.state.error} />
        }
        else {
            const rateLimitDisplay = <RateLimitDisplay graphQl={this.state.rateLimit.graphQl} rest={this.state.rateLimit.rest} sinceReload={this.state.rateLimit.sinceReload} />;
            const personViews = this.state.people.map(person =>
                <Person key={person} login={person} repoSet={this.props.repoSet} baseUrl={this.props.baseUrl} environment={this.props.environment} updateRateLimit={this.updateRateLimit.bind(this)} />);
            return <div className="tab-pane active">
                {this.props.environment === 'Development' ? rateLimitDisplay : ''}
                {personViews}
                {this.props.environment !== 'Development' ? rateLimitDisplay : ''}
            </div>;
        }
    }
};

class RateLimitDisplay extends React.Component<{ graphQl: { remaining: number, resetAt: string }, rest: { remaining: number, reset: string }, sinceReload: number }, undefined> {
    render() {
        if (this.props.graphQl && this.props.rest) {
            const gqlReset = new Date(this.props.graphQl.resetAt).toLocaleTimeString();
            const resetReset = new Date(this.props.rest.reset).toLocaleTimeString();
            return <div className="alert" style={{ marginBottom: '3em' }}>
                <h4>Rate limit status</h4>
                <div className="col-md-4"><strong>GraphQL:</strong> {this.props.graphQl.remaining} (Reset: {gqlReset})</div>
                <div className="col-md-4"><strong>REST:</strong> {this.props.rest.remaining} (Reset: {resetReset})</div>
                <div className="col-md-4"><strong>GraphQL Cost Since Reload:</strong> {this.props.sinceReload}</div>
            </div>;
        }
        else {
            return null;
        }
    }
}
