import * as React from 'react';
import * as ReactDOM from 'react-dom';

import * as Data from '../data';
import { Page } from './components';

document.addEventListener('DOMContentLoaded', async () => {
    // Find the app root and repo set name
    const appRoot = document.querySelector('#app-root');
    const repoSet = appRoot.getAttribute('data-repo-set');
    const baseUrl = appRoot.getAttribute('data-base-url');
    const environment = appRoot.getAttribute('data-environment');

    // Render the application
    ReactDOM.render(
        <Page repoSet={repoSet} baseUrl={baseUrl} environment={environment} />,
        appRoot);
});
