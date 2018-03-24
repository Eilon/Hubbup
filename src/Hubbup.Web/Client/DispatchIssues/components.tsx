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

class UserIcon extends React.Component<{ user: Data.User, small?: Boolean }, undefined> {
    render() {
        const size = this.props.small ? 16 : 32;
        return <a href={this.props.user.url}>
            <img style={{ margin: '0.1em' }} height={size} width={size} src={this.props.user.avatarUrl} title={this.props.user.login} alt={this.props.user.login} />
        </a>;
    }
}

class LabelView extends React.Component<{ label: Data.Label }, undefined> {
    render() {
        const style = {
            backgroundColor: `#${this.props.label.color}`,
            color: `#${this.props.label.foreColor}`,
            marginLeft: '3px'
        };
        return <span className="label pull-right" style={style}>
            {this.props.label.name}
        </span>;
    }
}

class AgeBadge extends React.Component<{ date: Date, timeAgo: string, prefix: string, stale: boolean }, undefined> {
    render() {
        const cssClass = `badge-pad badge pull-right${this.props.stale ? ' stale' : ''}`;
        return <span className={cssClass} title={`${this.props.prefix} on ${this.props.date}`}>
            {this.props.prefix} {this.props.timeAgo}
        </span>;
    }
}

class Badge extends React.Component<{ title: string }, undefined> {
    render() {
        return <span style={{ marginLeft: '3px' }} className="badge pull-right badge-pad">{this.props.title}</span>;
    }
}

interface IssueViewProps {
    issue: Data.Issue,
    repoLabels: string[],
    baseUrl: string
}

enum DispatchingState {
    None = 0,
    DispatchingInProgress,
    DispatchSuccess,
    DispatchError,
}

class IssueView extends React.Component<IssueViewProps, { selectedRepo: string, dispatchingState: DispatchingState }> {
    constructor(props: IssueViewProps) {
        super(props);

        this.state = {
            selectedRepo: '',
            dispatchingState: DispatchingState.None
        };

        this.handleRepoChange = this.handleRepoChange.bind(this);
    }

    async handleRepoChange(event: React.FormEvent<HTMLSelectElement>) {
        this.setState({ selectedRepo: event.currentTarget.value });

        this.setState({ dispatchingState: DispatchingState.DispatchingInProgress })

        try {
            const resp = await fetch(`${this.props.baseUrl}api/dispatchto/${this.props.issue.repository.owner.login}/${this.props.issue.repository.name}/${this.props.issue.number}/${event.currentTarget.value}`, fetchSettings);
            if (!resp.ok) {
                this.setState({ dispatchingState: DispatchingState.DispatchError })
            } else {
                this.setState({ dispatchingState: DispatchingState.DispatchSuccess })
            }
        } catch (e) {
            // Either a network error occurred or a JSON parse error
            this.setState({ dispatchingState: DispatchingState.DispatchError })
        }
    }

    render() {
        const issue = this.props.issue;

        const title = issue.milestone ? issue.milestone.title : '<no milestone>';
        let milestoneBadge = <Badge title={title} />;

        let dispatchStatus;

        switch (this.state.dispatchingState) {
            case DispatchingState.None:
                break;
            case DispatchingState.DispatchingInProgress:
                dispatchStatus =
                    <div className="progress" style={{ width: 64 }}>
                        <div className="progress-bar progress-bar-success progress-bar-striped" style={{ width: '50%' }}>
                        </div>
                    </div>;
                break;
            case DispatchingState.DispatchSuccess:
                dispatchStatus =
                    <div className="progress" style={{ width: 64 }}>
                        <div className="progress-bar progress-bar-success progress-bar-striped" style={{ width: '100%' }}>
                        </div>
                    </div>;
                break;
            case DispatchingState.DispatchError:
                dispatchStatus =
                    <div className="progress" style={{ width: 64 }}>
                        <div className="progress-bar progress-bar-danger progress-bar-striped" style={{ width: '100%' }}>
                        </div>
                    </div>;
                break;
        }

        let assigneeIcons;

        if (issue.assignees && issue.assignees.length > 0) {
            assigneeIcons =
                <span>
                    <span className="glyphicon glyphicon-arrow-right" />
                    {issue.assignees.map((assignee, i) => <UserIcon user={assignee} small={i > 0} />)}
                </span>
        } else {
            assigneeIcons =
                <span>
                </span>
        }

        return <li className="list-group-item">
            <div className="row">
                <div className="col-md-3" style={{ whiteSpace: 'nowrap' }}>
                    <UserIcon user={issue.author} />
                    {assigneeIcons}
                    <span className="issue-link">
                        <a href={issue.url}>
                            #{issue.number}
                        </a>
                    </span>
                </div>
                <div className="col-md-9">
                    <div>
                        <strong>{issue.title}</strong>
                        <span style={{ color: '#888', marginRight: '4px' }} className="pull-right">
                            <span style={{ marginRight: '4px' }} className="glyphicon glyphicon-comment"></span>
                            {issue.commentCount}
                        </span>
                    </div>

                    <span className="pull-right">
                        <select ref="repoLabelInput" defaultValue="" required value={this.state.selectedRepo} onChange={this.handleRepoChange}>
                            <option value="" disabled>Dispatch to...</option>
                            {
                                this.props.repoLabels.map(function (repoLabel) {
                                    return <option key={repoLabel}
                                        value={repoLabel}>{repoLabel}</option>;
                                })
                            }
                        </select>
                        {dispatchStatus}
                    </span>

                    {milestoneBadge}

                    {issue.labels.map(label => <LabelView key={label.id} label={label} />)}
                </div>
            </div>
        </li>;
    }
}

class IssueToDispatch extends React.Component<{ loading?: boolean, error?: string, issue: Data.Issue, repoLabels: string[], baseUrl: string }, undefined> {
    render() {
        return <div>
            <IssueView issue={this.props.issue} repoLabels={this.props.repoLabels} baseUrl={this.props.baseUrl} />
        </div>;
    }
};

interface PageProps {
    orgName: string,
    repoName: string,
    baseUrl: string
}

interface PageState {
    loading: boolean,
    error: string,
    data: Data.DispatchPageData
}

export class Page extends React.Component<PageProps, PageState> {
    constructor(props: PageProps) {
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

        // Fetch the list of issues and labels
        try {
            const resp = await fetch(`${this.props.baseUrl}api/dispatch/${this.props.orgName}/${this.props.repoName}`, fetchSettings);
            if (!resp.ok) {
                this.setState({
                    ... this.state,
                    loading: false,
                    error: `Unexpected response from server: ${resp.status} ${resp.statusText}`
                });
            } else {
                this.setState({
                    ... this.state,
                    loading: false,
                    error: '',
                    data: await resp.json()
                });
            }
        } catch (e) {
            // Either a network error occurred or a JSON parse error
            this.setState({
                ... this.state,
                loading: false,
                error: e.message,
                data: null
            });
        }
    }

    render() {
        let content;

        if (this.state.loading) {
            content = <div className="panel-body">
                <LoadingPanel />
            </div>;
        } else if (this.state.error) {
            content = <div className="panel-body">
                <ErrorPanel message={this.state.error} />
            </div>;
        } else {
            const issueViews = this.state.data.issuesWithoutRepoLabels.map(issue =>
                <IssueToDispatch key={issue.id} issue={issue} repoLabels={this.state.data.repoLabels} baseUrl={this.props.baseUrl} />);
            content = <div className="panel-body">
                {issueViews}
            </div>
        }

        const headerBadgeStyles = {
            marginLeft: '0.5em',
            marginTop: '-5px'
        };

        return <div className="panel panel-primary">
            <div className="panel-heading">
                <div className="pull-right">
                    <button className="btn btn-info btn-xs" style={headerBadgeStyles} onClick={() => this.reload()}>
                        <span className="glyphicon glyphicon-refresh" />
                    </button>
                </div>
                <h3 className="panel-title">
                    Issues to dispatch
                </h3>
            </div>
            {content}
        </div>;
    }
};
