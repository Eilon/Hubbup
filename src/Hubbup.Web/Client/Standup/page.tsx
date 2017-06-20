import * as React from 'react';
import * as ReactDOM from 'react-dom';

import * as Data from '../data';
import { Page } from './components';

document.addEventListener('DOMContentLoaded', async () => {
    // Find the app root and repo set name
    let appRoot = document.querySelector('#app-root');
    let repoSet = appRoot.getAttribute('data-repo-set');
    let baseUrl = appRoot.getAttribute('data-base-url');
    let environment = appRoot.getAttribute('data-environment');

    // Render the application
    ReactDOM.render(
        <Page repoSet={repoSet} baseUrl={baseUrl} environment={environment} />,
        appRoot);
});
