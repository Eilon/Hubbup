"use strict";
const webpack = require('webpack');
const path = require('path');
const ExtractTextPlugin = require('extract-text-webpack-plugin');
const CleanWebpackPlugin = require('clean-webpack-plugin');

const context = path.join(__dirname, 'Client');
const wwwroot = path.join(__dirname, 'wwwroot');
const dist = path.join(wwwroot, 'dist');

module.exports = {
    entry: {
        global: "./global.ts",
        standup: "./Standup/page.tsx"
    },
    context: context,
    devtool: 'inline-source-map',
    output: {
        filename: "[name].js",
        path: dist,
    },
    devServer: {
        contentBase: ".",
        host: "localhost",
        port: 9000,
    },
    resolve: {
        extensions: ['.ts', '.tsx', '.js']
    },
    module: {
        rules: [
            {
                test: /\.tsx?$/,
                loader: 'ts-loader',
                exclude: /node_modules/,
            },
            {
                test: /\.css$/,
                use: ExtractTextPlugin.extract({
                    use: 'css-loader',
                }),
            },
            {
                test: /\.(otf|eot|svg|ttf|woff2?)$/,
                loader: 'file-loader?name=[name].[hash].[ext]'
            }
        ],
    },
    plugins: [
        new CleanWebpackPlugin(dist),
        new ExtractTextPlugin('[name].css'),

        // Borrowed from https://webpack.js.org/guides/code-splitting-libraries/#implicit-common-vendor-chunk
        new webpack.optimize.CommonsChunkPlugin({
            name: 'vendor',
            minChunks: function (module) {
                // this assumes your vendor imports exist in the node_modules directory
                return module.context && module.context.indexOf('node_modules') !== -1;
            }
        }),

        function () {
            this.plugin("done", function (stats) {
                path.join()
            });
        },
    ],
};
