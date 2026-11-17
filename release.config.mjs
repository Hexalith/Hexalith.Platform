export default {
  branches: [{ name: '1.7.x', range: '1.7.x' }, 'main'],
  repositoryUrl: 'https://github.com/Hexalith/Hexalith.Platform.git',
  tagFormat: 'v${version}',
  plugins: [
    ['@semantic-release/commit-analyzer', { preset: 'conventionalcommits' }],
    ['@semantic-release/release-notes-generator', { preset: 'conventionalcommits' }],
    './eng/source-release/release-gate.mjs',
    ['@semantic-release/github', {
      assets: [], successComment: false, successCommentCondition: false,
      failComment: false, failTitle: false, failCommentCondition: false,
      labels: false, releasedLabels: false, addReleases: false, discussionCategoryName: false,
    }],
  ],
};
