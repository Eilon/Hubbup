import * as React from 'react';
import * as ReactDOM from 'react-dom';

import * as Data from '../data';
import { Page } from './components';

document.addEventListener('DOMContentLoaded', () => {
    // Find the app root and repo set name
    const appRoot = document.querySelector('#app-root');
    const orgName = appRoot.getAttribute('data-org-name');
    const repoName = appRoot.getAttribute('data-repo-name');
    const baseUrl = appRoot.getAttribute('data-base-url');

    // Render the application
    ReactDOM.render(
        <Page orgName={orgName} repoName={repoName} baseUrl={baseUrl} />,
        appRoot);
});
